using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress);
    Task<AuthResponseDto> GoogleLoginAsync(string idToken, string ipAddress);
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);
    Task RevokeTokenAsync(string refreshToken, Guid userId);
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordDto dto);
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken();
}