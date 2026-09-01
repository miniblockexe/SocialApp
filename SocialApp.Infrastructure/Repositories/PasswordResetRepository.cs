using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

public sealed class PasswordResetRepository : IPasswordResetRepository
{
    private readonly AppDbContext _db;

    public PasswordResetRepository(AppDbContext db) => _db = db;

    /// <summary>Tìm OTP hợp lệ — chưa used, chưa hết hạn.</summary>
    public async Task<PasswordResetToken?> GetActiveTokenAsync(string email, string otp)
    {
        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.User.Email == email &&
                t.Token == otp &&
                !t.IsUsed &&
                !t.IsCompleted &&
                t.ExpiresAt > DateTime.UtcNow);
    }

    /// <summary>Tìm record có VerifyToken hợp lệ — đã verify OTP, chưa đặt mật khẩu, chưa hết hạn.</summary>
    public async Task<PasswordResetToken?> GetByVerifyTokenAsync(string email, string verifyToken)
    {
        return await _db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.User.Email == email &&
                t.VerifyToken == verifyToken &&
                t.IsUsed == true && // OTP đã verify
                !t.IsCompleted && // chưa đặt mật khẩu
                t.VerifyTokenExpiresAt > DateTime.UtcNow);
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