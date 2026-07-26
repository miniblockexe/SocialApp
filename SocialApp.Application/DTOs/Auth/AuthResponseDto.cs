namespace SocialApp.Application.DTOs.Auth;

public sealed class AuthResponseDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
    public UserBriefDto User { get; init; } = new();
}