namespace SocialApp.Application.DTOs.Auth;

public sealed class ResetPasswordDto
{
    public string Email { get; init; } = string.Empty;
    /// <summary>Token tạm nhận được sau khi verify OTP thành công.</summary>
    public string VerifyToken { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmNewPassword { get; init; } = string.Empty;
}