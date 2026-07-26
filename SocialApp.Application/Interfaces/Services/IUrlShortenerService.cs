namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Rút gọn URL chia sẻ bài viết (TinyURL API — free).
/// Fail-open: trả URL gốc nếu API lỗi.
/// </summary>
public interface IUrlShortenerService
{
    /// <summary>Trả về URL rút gọn, hoặc longUrl nếu API thất bại.</summary>
    Task<string> ShortenAsync(string longUrl, CancellationToken ct = default);
}
