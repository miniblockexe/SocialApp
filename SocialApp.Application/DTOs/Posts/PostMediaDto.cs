using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO đại diện 1 file media (ảnh/video/audio) đính kèm bài đăng.
/// Field khớp tên với PostMediaFile entity nên map bằng AutoMapper convention, không cần ForMember.
/// </summary>
public sealed class PostMediaDto
{
    public Guid Id { get; init; }
    public string MediaUrl { get; init; } = string.Empty;
    public MediaType MediaType { get; init; }
    public StorageProvider StorageProvider { get; init; }

    /// <summary>Kích thước file tính bằng byte.</summary>
    public long FileSize { get; init; }
}