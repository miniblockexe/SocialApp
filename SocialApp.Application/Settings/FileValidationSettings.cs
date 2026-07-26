namespace SocialApp.Application.Settings;

/// <summary>
/// Strongly-typed settings cho validate file upload (ảnh/video/audio).
/// Bind từ appsettings.json section "FileValidationSettings" qua IOptions&lt;FileValidationSettings&gt;.
/// Dùng để tránh hardcode content-type / magic bytes trong service (đúng convention IOptions&lt;T&gt;).
/// </summary>
public sealed class FileValidationSettings
{
    public string[] AllowedImageContentTypes { get; init; } = [];
    public string[] AllowedVideoContentTypes { get; init; } = [];
    public string[] AllowedAudioContentTypes { get; init; } = [];

    /// <summary>
    /// Magic bytes dạng hex string, key = content type.
    /// Ví dụ: "image/jpeg" → "FFD8FF".
    /// </summary>
    public Dictionary<string, string> ImageMagicBytes { get; init; } = [];
}