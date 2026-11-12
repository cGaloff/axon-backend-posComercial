using Axon.API.Common;
using Axon.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Axon.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _permissions;

    // params: acepta uno o varios permisos. Con varios, basta con tener CUALQUIERA
    // de ellos (OR) — p. ej. un reporte accesible tanto con "reports:read" como
    // con el permiso específico del módulo que ya lo cubre ("sales:read").
    public RequirePermissionAttribute(params string[] permissions)
    {
        _permissions = permissions;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

        if (!_permissions.Any(currentUser.HasPermission))
        {
            // Mensaje para mostrar tal cual; el permiso exacto va en errors.
            context.Result = new ObjectResult(new ApiResponse<object>
            {
                Success = false,
                Message = "No tienes permiso para esta acción.",
                Errors = new List<string> { $"Permiso requerido: {string.Join(" o ", _permissions)}" },
                Code = ApiResponse<object>.ForbiddenCode
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
