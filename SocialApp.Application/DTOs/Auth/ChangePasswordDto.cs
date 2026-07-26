namespace SocialApp.Application.DTOs.Auth;

public sealed class ChangePasswordDto
{
    public string OldPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
    public string ConfirmNewPassword { get; init; } = string.Empty;
}