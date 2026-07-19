namespace Axon.API.DTOs.Users;

public record CreateUserRequest(
    string FullName,
    string Email,
    string Password,
    Guid RoleId);
