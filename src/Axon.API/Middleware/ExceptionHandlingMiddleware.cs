using Axon.Domain.Exceptions;
using FluentValidation;

namespace Axon.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Domain exception handled by middleware.");
            await WriteErrorResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access handled by middleware.");
            await WriteErrorResponseAsync(context, StatusCodes.Status401Unauthorized, "Unauthorized");
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation exception handled by middleware.");

            var errors = ex.Errors.Select(e => e.ErrorMessage).ToList();
            await WriteErrorResponseAsync(context, StatusCodes.Status400BadRequest, "Error de validación", errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception caught by middleware.");

            var message = _environment.IsDevelopment()
                ? ex.Message
                : "An unexpected error occurred.";

            await WriteErrorResponseAsync(context, StatusCodes.Status500InternalServerError, message);
        }
    }

    private static async Task WriteErrorResponseAsync(
        HttpContext context,
        int statusCode,
        string message,
        IEnumerable<string>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            throw new InvalidOperationException("The response has already started.");
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message,
            errors = errors ?? Array.Empty<string>()
        });
    }
}