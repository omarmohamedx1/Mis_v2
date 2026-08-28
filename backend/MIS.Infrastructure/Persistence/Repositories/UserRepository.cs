using Microsoft.EntityFrameworkCore;
using MIS.Application.Interfaces;
using MIS.Domain.Entities;

namespace MIS.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<User?> FindByLoginIdentifierAsync(string identifier, CancellationToken cancellationToken)
    {
        var normalizedLookup = identifier.Trim().ToLowerInvariant();

        return _dbContext.Users
            .Include(user => user.Department)
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role)
            .Include(user => user.AccessGrants)
            .FirstOrDefaultAsync(
                user => user.Username.ToLower() == normalizedLookup || user.Email.ToLower() == normalizedLookup || user.LoginCode.ToLower() == normalizedLookup,
                cancellationToken);
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => _dbContext.Users
        .Include(user => user.Department)
        .Include(user => user.UserRoles)
        .ThenInclude(userRole => userRole.Role)
        .Include(user => user.AccessGrants)
        .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, Guid excludingUserId, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return _dbContext.Users.AnyAsync(user => user.Id != excludingUserId && user.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
