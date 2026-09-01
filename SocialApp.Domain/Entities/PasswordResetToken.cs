namespace SocialApp.Domain.Entities;

public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }

    /// <summary>OTP 6 số gửi qua email.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token tạm sau khi verify OTP thành công.
    /// Dùng để xác thực bước đặt mật khẩu mới — hết hạn sau 5 phút.
    /// </summary>
    public string? VerifyToken { get; set; }

    /// <summary>Thời hạn OTP (15 phút).</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Thời hạn VerifyToken (5 phút sau khi verify OTP).</summary>
    public DateTime? VerifyTokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>OTP đã được verify — không dùng lại được.</summary>
    public bool IsUsed { get; set; } = false;

    /// <summary>Mật khẩu đã được đặt lại thành công.</summary>
    public bool IsCompleted { get; set; } = false;

    public User User { get; set; } = null!;
}