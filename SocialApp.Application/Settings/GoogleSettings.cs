namespace SocialApp.Application.Settings;

public sealed class GoogleSettings
{
    /// <summary>
    /// OAuth 2.0 Client ID từ Google Cloud Console.
    /// Dùng để verify audience trong ID token.
    /// </summary>
    public string ClientId { get; init; } = string.Empty;
}