namespace SocialApp.Application.DTOs.Cloud;

/// <summary>
/// Thông tin của một file đang lưu trữ trên Cloudflare R2.
/// Dùng cho listing và admin stats.
/// </summary>
public sealed class R2FileInfo
{
    /// <summary>S3 object key — dùng để xóa file.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>URL public để truy cập file.</summary>
    public string PublicUrl { get; init; } = string.Empty;

    /// <summary>Tên file không có folder prefix.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Kích thước file tính bằng bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Kích thước file tính bằng MB, làm tròn 2 chữ số thập phân.</summary>
    public double FileSizeMB => Math.Round(FileSize / 1024.0 / 1024.0, 2);

    /// <summary>Content type của file: image/jpeg, video/mp4, audio/mpeg...</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Thời điểm upload/chỉnh sửa lần cuối (UTC).</summary>
    public DateTime LastModified { get; init; }
}