namespace SocialApp.Application.Settings;

public sealed class GmailSettings
{
    public string SenderEmail { get; init; } = string.Empty;
    public string SenderName { get; init; } = "SocialApp";

    // Gmail API OAuth2
    public string OAuthClientId { get; init; } = string.Empty;
    public string OAuthClientSecret { get; init; } = string.Empty;
    public string OAuthRefreshToken { get; init; } = string.Empty;
}