namespace SocialApp.Application.DTOs.Auth;

/// <summary>
/// Thông tin bảo mật của CHÍNH user đang đăng nhập (không dùng cho profile người khác).
/// Tách riêng khỏi UserProfileDto để không lộ email của user khác.
/// </summary>
public sealed class AccountSecurityDto
{
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// False với tài khoản tạo qua Google và chưa từng đặt mật khẩu
    /// (PasswordHash rỗng) → FE dùng OTP để đặt mật khẩu lần đầu.
    /// </summary>
    public bool HasPassword { get; init; }
}
