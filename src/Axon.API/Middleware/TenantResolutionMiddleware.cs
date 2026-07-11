using Axon.API.Common;
using Axon.Infrastructure.MultiTenant;

namespace Axon.API.Middleware;

public class TenantResolutionMiddleware
{
    private const string TenantHeaderName = "X-Tenant-Slug";

    private static readonly string[] ExcludedPaths =
    {
        "/auth/register-tenant",
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

        if (ExcludedPaths.Any(excluded => path.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
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

        tenantContext.SetTenant(tenant.Slug, tenant.SchemaName);

        await _next(context);
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
