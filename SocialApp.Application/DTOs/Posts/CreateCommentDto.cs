namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// DTO tạo bình luận mới (hoặc reply vào 1 comment gốc).
/// </summary>
public sealed class CreateCommentDto
{
    /// <summary>Nội dung bình luận.</summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Null = tạo comment gốc trên bài viết.
    /// Có giá trị = reply vào comment gốc đó (chỉ hỗ trợ 1 cấp — reply vào reply bị chặn ở service, trả 400).
    /// </summary>
    public Guid? ParentCommentId { get; init; }
}