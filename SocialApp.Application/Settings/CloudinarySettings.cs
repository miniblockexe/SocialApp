namespace SocialApp.Application.Settings;

/// <summary>
/// Strongly-typed settings cho Cloudinary.
/// Bind từ appsettings.json section "CloudinarySettings" qua IOptions&lt;CloudinarySettings&gt;.
/// </summary>
public sealed class CloudinarySettings
{
    public string CloudName { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public string ApiSecret { get; init; } = string.Empty;
    public string AvatarFolder { get; init; } = "socialapp/avatars";
    public string PostImageFolder { get; init; } = "socialapp/posts";
    public long MaxImageSizeBytes { get; init; } = 10_485_760; // 10 MB
}