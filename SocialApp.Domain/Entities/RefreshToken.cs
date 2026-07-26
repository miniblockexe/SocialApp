namespace SocialApp.Domain.Entities;

/// <summary>
/// Refresh token cho JWT authentication.
/// Không kế thừa BaseAuditableEntity vì không cần soft-delete / UpdatedAt.
/// Revoke được xử lý qua RevokedAt + IsRevoked.
/// </summary>
public class RefreshToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Giá trị token — unique, dùng để tra cứu khi client gửi lên.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>FK → User sở hữu token này.</summary>
    public Guid UserId { get; set; }

    /// <summary>Thời điểm hết hạn (UTC) — mặc định 7 ngày từ lúc tạo.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Thời điểm bị thu hồi (UTC). Null = chưa bị revoke.</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Đánh dấu token đã bị thu hồi.
    /// Replay attack (dùng token 2 lần) → revoke toàn bộ token của user.
    /// </summary>
    public bool IsRevoked { get; set; } = false;

    // Audit

    /// <summary>IP address của thiết bị lúc đăng nhập — dùng để audit security.</summary>
    public string? IpAddress { get; set; }

    // Computed

    /// <summary>
    /// Token còn hiệu lực: chưa bị revoke VÀ chưa hết hạn.
    /// Computed property — không map xuống DB.
    /// </summary>
    public bool IsActive => !IsRevoked && DateTime.UtcNow < ExpiresAt;

    // Navigation properties

    /// <summary>User sở hữu token này.</summary>
    public User User { get; set; } = null!;
}
