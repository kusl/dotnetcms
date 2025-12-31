using Microsoft.EntityFrameworkCore;
using MyBlog.Core.Interfaces;
using MyBlog.Core.Models;
using MyBlog.Infrastructure.Data;

namespace MyBlog.Infrastructure.Repositories;

/// <summary>
/// SQLite implementation of the user repository.
/// </summary>
public sealed class UserRepository : IUserRepository
{
    private readonly BlogDbContext _context;

    public UserRepository(BlogDbContext context) => _context = context;

    /// <inheritdoc />
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
        => await _context.Users.FirstOrDefaultAsync(
            u => u.Username.ToLower() == username.ToLower(), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> AnyUsersExistAsync(CancellationToken cancellationToken = default)
        => await _context.Users.AnyAsync(cancellationToken);

    /// <inheritdoc />
    public async Task CreateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.FindAsync([id], cancellationToken);
        if (user is not null)
        {
            _context.Users.Remove(user);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
