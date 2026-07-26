namespace SocialApp.Application.DTOs.Auth;

public sealed class RefreshTokenRequestDto
{
    public string RefreshToken { get; init; } = string.Empty;
}