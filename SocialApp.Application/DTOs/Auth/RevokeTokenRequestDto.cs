namespace SocialApp.Application.DTOs.Auth;

public sealed class RevokeTokenRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}