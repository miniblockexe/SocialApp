namespace SocialApp.Application.DTOs.Messages;

/// <summary>
/// Preview bài viết được chia sẻ vào tin nhắn.
/// Chỉ chứa đủ dữ liệu để render card trong chat bubble —
/// không clone toàn bộ PostResponseDto để tránh payload nặng.
/// </summary>
public sealed class SharedPostPreviewDto
{
    /// <summary>Id bài viết gốc — dùng để navigate /posts/{PostId}.</summary>
    public Guid PostId { get; init; }

    /// <summary>Tên tác giả.</summary>
    public string AuthorName { get; init; } = string.Empty;

    /// <summary>Avatar tác giả.</summary>
    public string? AuthorAvatarUrl { get; init; }

    /// <summary>
    /// Đoạn nội dung tối đa 200 ký tự — nullable nếu bài chỉ có media.
    /// </summary>
    public string? ContentSnippet { get; init; }

    /// <summary>
    /// URL ảnh/video đầu tiên của bài — dùng làm thumbnail trong card.
    /// Null nếu bài không có media.
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// True = bài đã bị xóa sau khi được share.
    /// Client hiển thị "Bài viết đã bị xóa" thay vì card preview.
    /// </summary>
    public bool IsDeleted { get; init; }
}