namespace SocialApp.Application.DTOs.Posts;

/// <summary>
/// Query params cho GET /api/posts/feed.
/// Page/Size tự clamp về giá trị hợp lệ theo defensive coding rule
/// (Page &lt; 1 → 1, Size &lt; 1 → 10, Size &gt; 100 → 100) — cùng pattern với PagedQuery.
/// </summary>
public sealed class FeedQueryDto
{
    private int _page = 1;
    private int _size = 10;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int Size
    {
        get => _size;
        init => _size = value < 1 ? 10 : value > 100 ? 100 : value;
    }

    /// <summary>
    /// Id bài đăng làm mốc — lấy các bài CŨ HƠN bài này (cursor-based pagination, hiệu quả hơn OFFSET).
    /// Null = lấy từ đầu (trang mới nhất).
    /// </summary>
    public Guid? CursorId { get; init; }
}