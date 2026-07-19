using Axon.API.Common;
using Axon.API.DTOs.Users;
using Axon.API.Filters;
using Axon.Application.Users.Commands;
using Axon.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [RequirePermission("users:read")]
    public async Task<IActionResult> GetUsers([FromQuery] bool includeInactive = false)
    {
        var result = await _mediator.Send(new GetUsersQuery(includeInactive));

        return Ok(ApiResponse<List<UserDto>>.Ok(result));
    }

    [HttpGet("roles")]
    [RequirePermission("users:read")]
    public async Task<IActionResult> GetRoles()
    {
        var result = await _mediator.Send(new GetRolesQuery());

        return Ok(ApiResponse<List<RoleDto>>.Ok(result));
    }

    [HttpPost]
    [RequirePermission("users:write")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request)
    {
        var command = new CreateUserCommand(request.FullName, request.Email, request.Password, request.RoleId);

        var id = await _mediator.Send(command);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<Guid>.Ok(id, "Usuario creado exitosamente"));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("users:write")]
    public async Task<IActionResult> UpdateUser(Guid id, UpdateUserRequest request)
    {
        await _mediator.Send(new UpdateUserCommand(id, request.FullName, request.RoleId));

        return Ok(ApiResponse<string>.Ok("ok", "Usuario actualizado exitosamente"));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("users:write")]
    public async Task<IActionResult> DeactivateUser(Guid id)
    {
        await _mediator.Send(new DeactivateUserCommand(id));

        return Ok(ApiResponse<string>.Ok("ok", "Usuario desactivado exitosamente"));
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("users:write")]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        await _mediator.Send(new ReactivateUserCommand(id));

        return Ok(ApiResponse<string>.Ok("ok", "Usuario reactivado exitosamente"));
    }

    [HttpPost("{id:guid}/reset-password")]
    [RequirePermission("users:write")]
    public async Task<IActionResult> ResetPassword(Guid id, ChangeUserPasswordRequest request)
    {
        await _mediator.Send(new ChangeUserPasswordCommand(id, request.NewPassword));

        return Ok(ApiResponse<string>.Ok("ok", "Contraseña actualizada exitosamente"));
    }
}
