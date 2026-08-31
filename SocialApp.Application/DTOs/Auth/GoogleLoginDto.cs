namespace SocialApp.Application.DTOs.Auth;

/// <summary>
/// Client gửi idToken (từ Google Sign-In JS SDK hoặc @react-oauth/google).
/// Backend verify bằng Google tokeninfo endpoint.
/// </summary>
public sealed class GoogleLoginDto
{
    /// <summary>ID token từ Google OAuth 2.0 response.</summary>
    public string IdToken { get; init; } = string.Empty;
}