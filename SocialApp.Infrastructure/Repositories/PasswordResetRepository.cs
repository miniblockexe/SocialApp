using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
    private readonly AppDbContext _db;

    public PasswordResetRepository(AppDbContext db) => _db = db;

    public async Task<PasswordResetToken?> GetActiveTokenAsync(string email, string token)
    {
        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.User.Email == email &&
                t.Token == token &&
                !t.IsUsed &&
                t.ExpiresAt > DateTime.UtcNow);
    }

    public async Task DeleteAllForUserAsync(Guid userId)
    {
        var tokens = await _db.PasswordResetTokens
            .Where(t => t.UserId == userId)
            .ToListAsync();

        _db.PasswordResetTokens.RemoveRange(tokens);
    }

    public async Task AddAsync(PasswordResetToken token)
        => await _db.PasswordResetTokens.AddAsync(token);

    public Task SaveChangesAsync()
        => _db.SaveChangesAsync();
}