namespace SocialApp.Application.Settings;

/// <summary>
/// Strongly-typed config cho JWT.
/// Bind từ section "JwtSettings" trong appsettings.json.
/// Property names khớp với ServiceCollectionExtensions và AuthService.
/// </summary>
public sealed class JwtSettings
{
    /// <summary>Secret key dùng ký HS256 — tối thiểu 32 ký tự.</summary>
    public string SecretKey { get; init; } = string.Empty;

    /// <summary>Issuer của JWT token.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>Audience của JWT token.</summary>
    public string Audience { get; init; } = string.Empty;

    /// <summary>Thời gian sống của access token (phút). Mặc định 15 phút.</summary>
    public int AccessTokenExpirationMinutes { get; init; } = 15;

    /// <summary>Thời gian sống của refresh token (ngày). Mặc định 7 ngày.</summary>
    public int RefreshTokenExpirationDays { get; init; } = 7;
}