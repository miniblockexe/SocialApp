using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Request body để admin xóa file trực tiếp trên cloud storage.
/// Nếu PostMediaFileId có giá trị → xóa luôn record DB sau khi xóa cloud.
/// </summary>
public sealed class AdminDeleteCloudFileDto
{
    /// <summary>
    /// PublicId (Cloudinary) hoặc object key (R2) của file cần xóa.
    /// Bắt buộc, không được toàn whitespace.
    /// </summary>
    public string PublicIdOrKey { get; init; } = string.Empty;

    /// <summary>Provider đang lưu file: Cloudinary = 0, R2 = 1.</summary>
    public StorageProvider Provider { get; init; }

    /// <summary>Loại media: Image = 0, Video = 1, Audio = 2.</summary>
    public MediaType MediaType { get; init; }

    /// <summary>
    /// Id của PostMediaFile trong DB — nếu có giá trị thì xóa luôn record sau khi xóa cloud.
    /// Null hoặc Guid.Empty = chỉ xóa trên cloud, không xóa DB.
    /// </summary>
    public Guid? PostMediaFileId { get; init; }
}