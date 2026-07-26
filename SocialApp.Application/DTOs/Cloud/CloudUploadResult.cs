using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Cloud;

/// <summary>
/// Kết quả trả về sau khi upload file lên cloud (Cloudinary hoặc R2).
/// Dùng chung cho cả 2 provider — caller phân biệt qua StorageProvider.
/// </summary>
public sealed class CloudUploadResult
{
    /// <summary>URL public (https) để truy cập file.</summary>
    public string SecureUrl { get; init; } = string.Empty;

    /// <summary>
    /// Định danh dùng để xóa file sau này.
    /// Cloudinary: publicId (vd: "socialapp/avatars/abc123")
    /// R2: object key (vd: "videos/2024/abc123.mp4")
    /// </summary>
    public string PublicId { get; init; } = string.Empty;

    /// <summary>Kích thước file tính bằng bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Định dạng file: jpg, png, mp4, mp3...</summary>
    public string Format { get; init; } = string.Empty;

    /// <summary>Chiều rộng (px) — null nếu không phải ảnh.</summary>
    public int? Width { get; init; }

    /// <summary>Chiều cao (px) — null nếu không phải ảnh.</summary>
    public int? Height { get; init; }

    /// <summary>Provider đã lưu file: Cloudinary hoặc R2.</summary>
    public StorageProvider StorageProvider { get; init; }

    /// <summary>Loại media: Image, Video, Audio.</summary>
    public MediaType MediaType { get; init; }
}