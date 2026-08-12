using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>Body request khi chia sẻ lại bài viết (POST /api/posts/{id}/share-to-feed).</summary>
public sealed class SharePostRequestDto
{
    /// <summary>Caption tùy chọn đi kèm khi share (có thể để trống).</summary>
    public string? Content { get; init; }

    /// <summary>Quyền hiển thị của bài share. Mặc định Public.</summary>
    public PostPrivacy Privacy { get; init; } = PostPrivacy.Public;
}