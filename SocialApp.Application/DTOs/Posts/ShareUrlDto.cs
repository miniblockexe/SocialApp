namespace SocialApp.Application.DTOs.Posts;

/// <summary>Kết quả rút gọn URL chia sẻ bài đăng.</summary>
public sealed record ShareUrlDto(
    Guid PostId,
    /// <summary>URL gốc đầy đủ đến bài đăng.</summary>
    string LongUrl,
    /// <summary>URL rút gọn từ TinyURL — có thể bằng LongUrl nếu API lỗi.</summary>
    string ShortUrl);
