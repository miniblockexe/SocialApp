using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Implementation của IUserRepository.
/// Kế thừa GenericRepository để có sẵn CRUD cơ bản.
/// Chỉ chứa truy vấn DB — không có business logic.
/// </summary>
public sealed class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    /// <inheritdoc/>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var emailNormalized = email.Trim().ToLower();

        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == emailNormalized, ct);
    }

    /// <inheritdoc/>
    public async Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;

        var usernameLower = username.Trim().ToLower();

        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == usernameLower, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var emailNormalized = email.Trim().ToLower();

        return await _dbSet.AnyAsync(u => u.Email == emailNormalized, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return false;

        var usernameLower = username.Trim().ToLower();

        return await _dbSet.AnyAsync(u => u.Username.ToLower() == usernameLower, ct);
    }
}