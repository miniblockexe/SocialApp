namespace SocialApp.Domain.Entities;

/// <summary>
/// Token dùng để reset mật khẩu — gửi qua email, có TTL 15 phút.
/// Một user chỉ giữ 1 token active tại một thời điểm;
/// service xoá token cũ trước khi tạo mới.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → User yêu cầu reset.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Token 6 chữ số — dễ gõ trên mobile.
    /// Hoặc dùng Guid-based token nếu muốn link click.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Thời điểm hết hạn (UTC) — mặc định UtcNow + 15 phút.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Đã sử dụng rồi — dùng 1 lần, sau đó IsUsed = true.</summary>
    public bool IsUsed { get; set; } = false;

    // Navigation
    public User User { get; set; } = null!;
}