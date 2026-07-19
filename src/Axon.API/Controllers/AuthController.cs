using Axon.API.Common;
using Axon.API.DTOs.Auth;
using Axon.Application.Auth.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Axon.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var command = new LoginCommand(request.Email, request.Password, request.TenantSlug);
        var result = await _mediator.Send(command);

        return Ok(ApiResponse<LoginResult>.Ok(result, "Login exitoso"));
    }
}
