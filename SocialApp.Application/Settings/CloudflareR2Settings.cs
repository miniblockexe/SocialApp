namespace SocialApp.Application.Settings;

/// <summary>
/// Strongly-typed settings cho Cloudflare R2 (video/audio storage).
/// Bind từ appsettings.json section "CloudflareR2Settings" qua IOptions&lt;CloudflareR2Settings&gt;.
/// </summary>
public sealed class CloudflareR2Settings
{
    public string AccountId { get; init; } = string.Empty;
    public string AccessKeyId { get; init; } = string.Empty;
    public string SecretAccessKey { get; init; } = string.Empty;
    public string BucketName { get; init; } = string.Empty;
    public string PublicUrl { get; init; } = string.Empty;
    public string VideoFolder { get; init; } = "videos";
    public string AudioFolder { get; init; } = "audio";
    public long MaxVideoSizeBytes { get; init; } = 524_288_000; // 500 MB
    public long MaxAudioSizeBytes { get; init; } = 52_428_800;  // 50 MB
    public string ServiceUrl => $"https://{AccountId}.r2.cloudflarestorage.com";
}