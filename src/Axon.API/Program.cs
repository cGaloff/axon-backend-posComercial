using System.Text;
using System.Text.Json.Serialization;
using Axon.API.Middleware;
using Axon.Application.Auth.Commands;
using Axon.Application.Common.Behaviors;
using Axon.Application.Interfaces;
using Axon.Domain.Interfaces;
using Axon.Infrastructure.MultiTenant;
using Axon.Infrastructure.Persistence;
using Axon.Infrastructure.Persistence.Repositories;
using Axon.Infrastructure.Security;
using Axon.Infrastructure.Services;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using QuestPDF.Infrastructure;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options => 
        options.JsonSerializerOptions.Converters
            .Add(new JsonStringEnumConverter()));

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Autorización JWT. Ingresa: Bearer {token}"
    });

    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", document),
            new List<string>()
        }
    });
});

builder.Services.AddMemoryCache();

var masterConnectionString = builder.Configuration.GetConnectionString("MasterDb")
    ?? throw new InvalidOperationException("La cadena de conexión 'MasterDb' no está configurada.");

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(masterConnectionString));
builder.Services.AddScoped<IMasterDbContext>(sp => sp.GetRequiredService<AppDbContext>());

// TenantDbContext se conecta a la misma base física (multi-tenant por schema);
// el cambio de schema por request lo hace TenantSchemaInterceptor, ya registrado
// dentro de TenantDbContext.OnConfiguring a partir del ITenantContext inyectado.
builder.Services.AddDbContext<TenantDbContext>(options => options.UseNpgsql(masterConnectionString));
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TenantDbContext>());

// Se registra el tipo concreto porque TenantResolutionMiddleware necesita
// llamar TenantContext.SetTenant(), que no forma parte de ITenantContext.
// ITenantContext se mapea a la misma instancia por scope para el resto del código.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<TenantSchemaInitializer>();
builder.Services.AddScoped<ITenantSchemaInitializer>(sp => sp.GetRequiredService<TenantSchemaInitializer>());
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Security
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();

// Sales
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<IEmailService, EmailService>();

// Cash Register
builder.Services.AddScoped<ICashSessionRepository, CashSessionRepository>();

// Configuración del tenant
builder.Services.AddScoped<ITenantConfigRepository, TenantConfigRepository>();

var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSettings["Key"]
    ?? throw new InvalidOperationException("La clave 'Jwt:Key' no está configurada.");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    // Sin esto, el validador de JWT renombra automaticamente claims estandar
    // (sub -> ClaimTypes.NameIdentifier, email -> ClaimTypes.Email, role ->
    // ClaimTypes.Role) antes de exponerlos en HttpContext.User, dejando
    // ICurrentUserContext.UserId/Email/Role siempre vacios porque busca los
    // nombres cortos originales ("sub", "email", "role").
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

var applicationAssembly = typeof(LoginCommand).Assembly;

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

// CORS: en prod se define Cors:AllowedOrigins (CSV) para restringir a los dominios
// del frontend. Si no hay orígenes configurados (dev/prueba), se abre a cualquiera.
// AllowAnyOrigin NO es compatible con AllowCredentials, pero la API usa JWT en el
// header Authorization (no cookies), así que no hace falta.
var corsOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Detrás de Nginx (u otro proxy) que termina TLS: respeta X-Forwarded-Proto/For
// para que el scheme/IP real lleguen a la app (necesario para HttpsRedirection y
// para no romper el flujo HTTPS). Se limpian las redes/proxies conocidos porque el
// proxy llega por la red interna de Docker, no desde loopback.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Aplica las migraciones pendientes de la base master al arrancar.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseHttpsRedirection();

// CORS debe ir ANTES del TenantResolutionMiddleware: el preflight OPTIONS del
// navegador no incluye el header X-Tenant-Slug, así que si el middleware de tenant
// corre primero lo rechazaría con 400 y rompería CORS. UseCors responde el preflight
// aquí y lo corta antes de llegar a la resolución de tenant.
app.UseCors();

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Orden del pipeline:
// 1. ForwardedHeaders             -> scheme/IP real desde el reverse proxy
// 2. ExceptionHandlingMiddleware  -> captura cualquier excepción de las etapas siguientes
// 3. Swagger / SwaggerUI          -> solo en Development
// 4. HttpsRedirection
// 5. CORS                         -> antes del tenant para no romper el preflight OPTIONS
// 6. TenantResolutionMiddleware   -> resuelve el tenant antes de auth/negocio
// 7. Authentication
// 8. Authorization
// 9. MapControllers
