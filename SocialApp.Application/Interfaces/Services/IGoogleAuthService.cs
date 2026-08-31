namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Verify Google ID token và trả về thông tin user từ payload.
/// </summary>
public interface IGoogleAuthService
{
    /// <summary>
    /// Verify idToken với Google.
    /// Returns null nếu token không hợp lệ.
    /// </summary>
    Task<GoogleUserInfo?> VerifyIdTokenAsync(string idToken);
}

public sealed class GoogleUserInfo
{
    public string GoogleId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Picture { get; init; }
    public bool EmailVerified { get; init; }
}