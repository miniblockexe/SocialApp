using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO đầy đủ thông tin 1 bài đăng — trả về khi xem chi tiết, feed, hoặc danh sách bài của user.
/// LikeCount, CommentCount, IsLikedByMe, IsOwner, ShareCount, IsSharedByMe được tính thủ công
/// trong service, không map qua AutoMapper (giống pattern UserProfileDto).
/// </summary>
public sealed class PostResponseDto
{
    public Guid Id { get; init; }
    public string? Content { get; init; }
    public PostPrivacy Privacy { get; init; }

    /// <summary>Thời điểm tạo (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Thời điểm cập nhật gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; init; }

    /// <summary>Tác giả bài đăng.</summary>
    public UserBriefDto Author { get; init; } = null!;

    /// <summary>Danh sách file media đính kèm.</summary>
    public List<PostMediaDto> MediaFiles { get; init; } = [];

    /// <summary>Số lượt thích.</summary>
    public int LikeCount { get; set; }

    /// <summary>Số bình luận (không tính đã xoá).</summary>
    public int CommentCount { get; set; }

    /// <summary>Viewer hiện tại đã like bài này chưa.</summary>
    public bool IsLikedByMe { get; set; }

    /// <summary>Viewer hiện tại có phải tác giả bài này không.</summary>
    public bool IsOwner { get; set; }

    /// <summary>Số lần bài này được chia sẻ lại (repost).</summary>
    public int ShareCount { get; set; }

    /// <summary>Viewer hiện tại đã chia sẻ lại bài này chưa.</summary>
    public bool IsSharedByMe { get; set; }

    /// <summary>
    /// Bài gốc được nhúng bên trong 
    /// Null = bài đăng thông thường.
    /// </summary>
    public OriginalPostDto? OriginalPost { get; set; }
}