using MediatR;

namespace Axon.Application.Users.Queries;

public record GetRolesQuery : IRequest<List<RoleDto>>;

public record RoleDto(
    Guid Id,
    string Name,
    string Description,
    bool IsSystem,
    List<string> Permissions);
