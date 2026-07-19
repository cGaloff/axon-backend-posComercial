using MediatR;

namespace Axon.Application.Users.Queries;

public record GetUsersQuery(bool IncludeInactive = false) : IRequest<List<UserDto>>;

public record UserDto(
    Guid Id,
    string FullName,
    string Email,
    Guid RoleId,
    string RoleName,
    bool IsActive,
    DateTime CreatedAt);
