namespace Axon.Application.Auth.Commands;

public record LoginResult(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    string UserFullName,
    string Role,
    IEnumerable<string> Permissions);
