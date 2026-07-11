using Axon.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Axon.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
}
