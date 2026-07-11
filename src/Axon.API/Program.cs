using System.Text;
using Axon.API.Middleware;
using Axon.Application;
using Axon.Domain.Interfaces;
using Axon.Infrastructure.MultiTenant;
using Axon.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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

// TenantDbContext se conecta a la misma base física (multi-tenant por schema);
// el cambio de schema por request lo hace TenantSchemaInterceptor, ya registrado
// dentro de TenantDbContext.OnConfiguring a partir del ITenantContext inyectado.
builder.Services.AddDbContext<TenantDbContext>(options => options.UseNpgsql(masterConnectionString));

// Se registra el tipo concreto porque TenantResolutionMiddleware necesita
// llamar TenantContext.SetTenant(), que no forma parte de ITenantContext.
// ITenantContext se mapea a la misma instancia por scope para el resto del código.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());

builder.Services.AddScoped<TenantResolver>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

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

var applicationAssembly = typeof(Class1).Assembly;

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(applicationAssembly));
builder.Services.AddValidatorsFromAssembly(applicationAssembly);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

// Orden del pipeline:
// 1. ExceptionHandlingMiddleware  -> captura cualquier excepción de las etapas siguientes
// 2. Swagger / SwaggerUI          -> solo en Development
// 3. HttpsRedirection
// 4. TenantResolutionMiddleware   -> resuelve el tenant antes de auth/negocio
// 5. Authentication
// 6. Authorization
// 7. MapControllers
