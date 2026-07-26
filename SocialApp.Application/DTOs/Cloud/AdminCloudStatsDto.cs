namespace SocialApp.Application.DTOs.Cloud;

/// <summary>
/// DTO tổng hợp thống kê cloud storage cho admin dashboard.
/// Gộp cả Cloudinary và R2 vào một response duy nhất.
/// </summary>
public sealed class AdminCloudStatsDto
{
    /// <summary>Dung lượng đã dùng trên Cloudinary (MB).</summary>
    public double CloudinaryUsageMB { get; init; }

    /// <summary>Dung lượng đã dùng trên Cloudinary (GB), làm tròn 2 chữ số thập phân.</summary>
    public double CloudinaryUsageGB => Math.Round(CloudinaryUsageMB / 1024.0, 2);

    /// <summary>Giới hạn dung lượng của plan Cloudinary (MB). Free plan = 25GB = 25600MB.</summary>
    public double CloudinaryPlanLimitMB { get; init; } = 25600;

    /// <summary>Phần trăm dung lượng đã dùng so với giới hạn plan, làm tròn 1 chữ số thập phân.</summary>
    public double CloudinaryUsagePercent =>
        Math.Round(CloudinaryPlanLimitMB > 0
            ? CloudinaryUsageMB / CloudinaryPlanLimitMB * 100
            : 0, 1);

    /// <summary>Thống kê tổng quan storage R2.</summary>
    public R2StorageStats R2Stats { get; init; } = new();

    /// <summary>20 file mới nhất trên R2 (sắp xếp theo LastModified DESC).</summary>
    public List<R2FileInfo> RecentR2Files { get; init; } = [];
}