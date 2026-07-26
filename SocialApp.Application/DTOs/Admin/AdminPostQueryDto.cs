namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Query params để admin lọc và phân trang danh sách bài đăng.
/// Bind từ query string — defensive: page/size tự clamp, SortBy validate trong service.
/// </summary>
public sealed class AdminPostQueryDto
{
    private int _page = 1;
    private int _size = 20;

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
    /// Lọc theo trạng thái xóa:
    /// null = tất cả, true = chỉ bài đã xóa, false = chỉ bài còn hoạt động.
    /// </summary>
    public bool? IsDeleted { get; init; }

    /// <summary>Lọc theo tác giả — null hoặc Guid.Empty = không lọc.</summary>
    public Guid? UserId { get; init; }

    /// <summary>Tìm kiếm trong Content (case-insensitive) — null = không lọc.</summary>
    public string? Keyword { get; init; }

    /// <summary>Lọc bài đăng từ ngày này (UTC inclusive) — null = không giới hạn.</summary>
    public DateTime? FromDate { get; init; }

    /// <summary>Lọc bài đăng đến ngày này (UTC inclusive) — null = không giới hạn.</summary>
    public DateTime? ToDate { get; init; }

    /// <summary>
    /// Field để sort. Giá trị hợp lệ: "createdAt" | "likeCount" | "commentCount".
    /// Giá trị không hợp lệ → service tự fallback về "createdAt".
    /// </summary>
    public string SortBy { get; init; } = "createdAt";

    /// <summary>true = giảm dần (mới nhất trước), false = tăng dần. Default true.</summary>
    public bool SortDesc { get; init; } = true;
}