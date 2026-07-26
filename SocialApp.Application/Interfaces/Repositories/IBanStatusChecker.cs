namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Contract kiểm tra trạng thái ban của user.
/// Implement ở Infrastructure layer (BanStatusChecker).
/// Dùng trong BannedUserMiddleware sau khi đã authenticate.
/// </summary>
public interface IBanStatusChecker
{
    /// <summary>
    /// Kiểm tra user có đang bị ban không.
    /// Trả false nếu userId là Guid.Empty hoặc user không tồn tại.
    /// </summary>
    Task<bool> IsUserBannedAsync(Guid userId, CancellationToken ct = default);
}