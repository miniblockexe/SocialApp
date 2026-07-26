namespace SocialApp.Application.DTOs.Cloud;

/// <summary>
/// Thống kê tổng quan storage trên Cloudflare R2.
/// Dùng cho admin dashboard.
/// </summary>
public sealed class R2StorageStats
{
    /// <summary>Tổng số file đang lưu trữ.</summary>
    public int TotalFiles { get; init; }

    /// <summary>Tổng dung lượng tính bằng bytes.</summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>Tổng dung lượng tính bằng MB, làm tròn 2 chữ số thập phân.</summary>
    public double TotalSizeMB => Math.Round(TotalSizeBytes / 1024.0 / 1024.0, 2);

    /// <summary>Tổng dung lượng tính bằng GB, làm tròn 2 chữ số thập phân.</summary>
    public double TotalSizeGB => Math.Round(TotalSizeBytes / 1024.0 / 1024.0 / 1024.0, 2);

    /// <summary>
    /// Phân loại file theo content type prefix.
    /// Key: "image" | "video" | "audio" | "other"
    /// Value: số lượng file thuộc loại đó.
    /// </summary>
    public Dictionary<string, int> FilesByType { get; init; } = new();
}