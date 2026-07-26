using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

/// <summary>
/// File media đính kèm vào bài đăng (ảnh / video / audio).
/// Không kế thừa BaseAuditableEntity — không cần soft-delete / UpdatedAt.
/// Xoá theo cascade khi Post bị xoá.
/// </summary>
public class PostMediaFile
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK → Post chứa file này.</summary>
    public Guid PostId { get; set; }

    /// <summary>URL truy cập file (Cloudinary hoặc Cloudflare R2).</summary>
    public string MediaUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public ID trên storage provider — dùng để xoá file khi cần.
    /// Cloudinary: "folder/filename". R2: object key.
    /// </summary>
    public string PublicId { get; set; } = string.Empty;

    /// <summary>Loại media: Image = 0, Video = 1, Audio = 2.</summary>
    public MediaType MediaType { get; set; }

    /// <summary>Nơi lưu trữ: Cloudinary = 0 (ảnh), R2 = 1 (video/audio).</summary>
    public StorageProvider StorageProvider { get; set; }

    /// <summary>Kích thước file tính bằng byte.</summary>
    public long FileSize { get; set; }

    /// <summary>Thời điểm upload (UTC).</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties

    /// <summary>Bài đăng chứa file này.</summary>
    public Post Post { get; set; } = null!;
}