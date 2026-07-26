using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Implementation của IRefreshTokenRepository.
/// Không kế thừa GenericRepository vì RefreshToken không kế thừa BaseAuditableEntity.
/// Chỉ chứa truy vấn DB — không có business logic.
/// </summary>
public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly AppDbContext _context;

    public RefreshTokenRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;

        return await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == token, ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return [];

        var now = DateTime.UtcNow;

        return await _context.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > now)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<RefreshToken>> GetNonRevokedTokensByUserIdAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return [];

        return await _context.RefreshTokens
            .Where(t => t.UserId == userId && !t.IsRevoked)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);
    }

    /// <inheritdoc/>
    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        await _context.RefreshTokens.AddAsync(refreshToken, ct);
    }

    /// <inheritdoc/>
    public void Update(RefreshToken refreshToken)
    {
        _context.RefreshTokens.Update(refreshToken);
    }

    /// <inheritdoc/>
    public void UpdateRange(IEnumerable<RefreshToken> refreshTokens)
    {
        _context.RefreshTokens.UpdateRange(refreshTokens);
    }

    /// <inheritdoc/>
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}