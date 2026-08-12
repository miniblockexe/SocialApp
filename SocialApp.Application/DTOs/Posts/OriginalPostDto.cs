using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// Snapshot bài viết gốc được nhúng trong bài chia sẻ lại.
/// </summary>
public sealed class OriginalPostDto
{
    public Guid Id { get; init; }
    public string? Content { get; init; }
    public DateTime CreatedAt { get; init; }

    /// <summary>Tác giả bài gốc.</summary>
    public UserBriefDto Author { get; init; } = null!;

    /// <summary>Media của bài gốc.</summary>
    public List<PostMediaDto> MediaFiles { get; init; } = [];

    /// <summary>
    /// True nếu bài gốc đã bị xóa (soft-delete).
    /// FE hiển thị placeholder "Bài viết gốc đã bị xóa" thay vì nội dung.
    /// </summary>
    public bool IsDeleted { get; init; }
}