using Axon.API.Common;
using Axon.Infrastructure.MultiTenant;

namespace Axon.API.Middleware;

public class TenantResolutionMiddleware
{
    private const string TenantHeaderName = "X-Tenant-Slug";

    private static readonly string[] ExcludedPaths =
    {
        "/auth/register-tenant",
        "/api/tenants/register",
        "/api/subscription/extend",
        "/api/webhooks",
        "/health"
    };

    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, TenantResolver tenantResolver, TenantContext tenantContext)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (ExcludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)) ||
            IsSubscriptionEndpoint(path))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValues) ||
            string.IsNullOrWhiteSpace(headerValues.ToString()))
        {
            await WriteResponseAsync(context, StatusCodes.Status400BadRequest,
                ApiResponse<object>.Fail("El header X-Tenant-Slug es requerido."));
            return;
        }

        var slug = headerValues.ToString();
        var tenant = await tenantResolver.ResolveAsync(slug);

        if (tenant is null)
        {
            await WriteResponseAsync(context, StatusCodes.Status404NotFound,
                ApiResponse<object>.Fail("Tenant no encontrado."));
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteResponseAsync(context, StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Tenant suspendido."));
            return;
        }

        // El corte se decide aquí y no en la landing: si su cron falla, el
        // acceso se bloquea igual. Mensaje distinto al de suspensión para que
        // el cliente sepa que basta con renovar.
        if (tenant.IsSubscriptionExpired(DateTime.UtcNow))
        {
            await WriteResponseAsync(context, StatusCodes.Status403Forbidden,
                ApiResponse<object>.Fail("Suscripción vencida."));
            return;
        }

        tenantContext.SetTenant(tenant.Slug, tenant.SchemaName);

        await _next(context);
    }

    // PATCH /api/tenants/{slug}/subscription lleva el slug en la ruta y se
    // autentica con el secreto de aprovisionamiento. Además tiene que poder
    // renovar un tenant ya vencido, así que no puede pasar por este filtro.
    private static bool IsSubscriptionEndpoint(string path)
    {
        return path.StartsWith("/api/tenants/", StringComparison.OrdinalIgnoreCase) &&
               path.EndsWith("/subscription", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, ApiResponse<object> response)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(response);
    }
}
