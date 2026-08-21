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
        // Detrás de Nginx con ForwardedHeaders ya configurado (ver Program.cs),
        // RemoteIpAddress ya refleja la IP real del cliente, no la del proxy.
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var command = new LoginCommand(request.Email, request.Password, request.TenantSlug, ipAddress);
        var result = await _mediator.Send(command);

        return Ok(ApiResponse<LoginResult>.Ok(result, "Login exitoso"));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var result = await _mediator.Send(new RefreshTokenCommand(request.RefreshToken));

        return Ok(ApiResponse<LoginResult>.Ok(result, "Token renovado"));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(RefreshTokenRequest request)
    {
        await _mediator.Send(new LogoutCommand(request.RefreshToken));

        return Ok(ApiResponse<string>.Ok("ok", "Sesión cerrada"));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        await _mediator.Send(new ForgotPasswordCommand(request.Email, request.TenantSlug));

        // Mensaje siempre igual, exista o no el email: distinguirlo permitiría
        // usar este endpoint para enumerar correos registrados.
        return Ok(ApiResponse<string>.Ok("ok", "Si el correo existe, se envió un enlace de recuperación."));
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        await _mediator.Send(new ResetPasswordCommand(request.Token, request.NewPassword, request.TenantSlug));

        return Ok(ApiResponse<string>.Ok("ok", "Contraseña actualizada"));
    }
}
