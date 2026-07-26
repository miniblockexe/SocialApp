using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Contract cho RefreshToken repository.
/// Không kế thừa IGenericRepository vì RefreshToken không kế thừa BaseAuditableEntity
/// (không cần soft-delete, không cần CreatedAt/UpdatedAt auto-set).
/// </summary>
public interface IRefreshTokenRepository
{
    /// <summary>
    /// Tìm refresh token theo giá trị token string.
    /// Include navigation property User.
    /// Trả null nếu không tìm thấy.
    /// </summary>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Lấy tất cả refresh token còn active (chưa revoke, chưa hết hạn) của một user.
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetActiveTokensByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy tất cả refresh token chưa bị revoke của một user (bất kể còn hạn hay không).
    /// Dùng khi cần revoke toàn bộ session (đổi mật khẩu, replay attack).
    /// </summary>
    Task<IReadOnlyList<RefreshToken>> GetNonRevokedTokensByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Thêm refresh token mới vào DB.
    /// </summary>
    Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Cập nhật refresh token (revoke, set RevokedAt).
    /// </summary>
    void Update(RefreshToken refreshToken);

    /// <summary>
    /// Cập nhật nhiều refresh token cùng lúc (bulk revoke).
    /// </summary>
    void UpdateRange(IEnumerable<RefreshToken> refreshTokens);

    /// <summary>
    /// Lưu tất cả thay đổi vào DB.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}