using System.Text.Json.Serialization;

namespace Axon.API.Common;

public class ApiResponse<T>
{
    // Códigos de error estables para que el cliente decida sin depender del texto.
    public const string ForbiddenCode = "FORBIDDEN";

    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public List<string> Errors { get; init; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Code { get; init; }

    public static ApiResponse<T> Ok(T data, string message = "")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message }
        };
    }

    public static ApiResponse<T> Fail(string message, string code)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = new List<string> { message },
            Code = code
        };
    }

    public static ApiResponse<T> Fail(List<string> errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = errors.FirstOrDefault() ?? string.Empty,
            Errors = errors
        };
    }
}
