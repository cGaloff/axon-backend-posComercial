namespace Axon.API.DTOs.Users;

public record UpdateUserRequest(string FullName, Guid RoleId);
