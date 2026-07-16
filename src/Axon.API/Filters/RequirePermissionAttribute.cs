using Axon.API.Common;
using Axon.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Axon.API.Filters;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permission;

    public RequirePermissionAttribute(string permission)
    {
        _permission = permission;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var currentUser = context.HttpContext.RequestServices.GetRequiredService<ICurrentUserContext>();

        if (!currentUser.HasPermission(_permission))
        {
            context.Result = new ObjectResult(ApiResponse<object>.Fail($"No tienes permiso para: {_permission}"))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }
    }
}
