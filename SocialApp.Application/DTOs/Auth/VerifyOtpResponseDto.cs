namespace SocialApp.Application.DTOs.Auth;

public sealed class VerifyOtpResponseDto
{
    /// <summary>Token tạm dùng để đặt mật khẩu mới — hết hạn sau 5 phút.</summary>
    public string VerifyToken { get; init; } = string.Empty;
}