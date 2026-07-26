namespace SocialApp.Application.Common;

/// <summary>
/// Wrapper phân trang — dùng làm T trong ApiResponse&lt;PagedResult&lt;TItem&gt;&gt;.
/// </summary>
/// <typeparam name="TItem">Kiểu phần tử trong danh sách.</typeparam>
public sealed class PagedResult<TItem>
{
    /// <summary>Danh sách items của trang hiện tại.</summary>
    public IReadOnlyList<TItem> Items { get; init; } = [];

    /// <summary>Tổng số bản ghi trong toàn bộ kết quả (không phân trang).</summary>
    public int TotalCount { get; init; }

    /// <summary>Trang hiện tại (1-based).</summary>
    public int PageNumber { get; init; }

    /// <summary>Số item mỗi trang.</summary>
    public int PageSize { get; init; }

    /// <summary>Tổng số trang.</summary>
    public int TotalPages => PageSize > 0
        ? (int)Math.Ceiling(TotalCount / (double)PageSize)
        : 0;

    /// <summary>Có trang trước không?</summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>Có trang tiếp theo không?</summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>Trang hiện tại có data không?</summary>
    public bool IsEmpty => Items.Count == 0;

    // Private constructor — bắt buộc dùng factory methods

    private PagedResult() { }

    // Factory

    /// <summary>
    /// Tạo PagedResult từ danh sách đã được skip/take ở tầng repository.
    /// Tự động clamp page/size theo defensive coding rules.
    /// </summary>
    /// <param name="items">Items của trang hiện tại (đã query từ DB).</param>
    /// <param name="totalCount">Tổng số bản ghi (COUNT(*) trước skip/take).</param>
    /// <param name="pageNumber">Trang yêu cầu — tự sửa về 1 nếu &lt; 1.</param>
    /// <param name="pageSize">Kích thước trang — clamp [1, 100].</param>
    public static PagedResult<TItem> Create(
        IEnumerable<TItem> items,
        int totalCount,
        int pageNumber,
        int pageSize)
    {
        // Defensive coding — không throw, tự sửa về giá trị hợp lệ
        var safePage = pageNumber < 1 ? 1 : pageNumber;
        var safeSize = pageSize < 1 ? 10 : pageSize > 100 ? 100 : pageSize;
        var safeTotalCount = totalCount < 0 ? 0 : totalCount;

        return new PagedResult<TItem>
        {
            Items = items.ToList().AsReadOnly(),
            TotalCount = safeTotalCount,
            PageNumber = safePage,
            PageSize = safeSize
        };
    }

    /// <summary>
    /// Tạo PagedResult rỗng — dùng khi query không có kết quả nào.
    /// </summary>
    public static PagedResult<TItem> Empty(int pageNumber = 1, int pageSize = 10)
        => Create([], 0, pageNumber, pageSize);
}

/// <summary>
/// Query params phân trang — bind từ query string.
/// Áp dụng defensive coding: page &lt; 1 → 1, size out of range → clamp.
/// </summary>
public sealed class PagedQuery
{
    // Backing fields — không dùng C# 13 `field` keyword vì project target net8.0
    private int _pageNumber = 1;
    private int _pageSize = 10;
    private string? _search;

    public int PageNumber
    {
        get => _pageNumber;
        init => _pageNumber = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value < 1 ? 10 : value > 100 ? 100 : value;
    }

    /// <summary>Từ khoá tìm kiếm — tự trim, null nếu toàn whitespace.</summary>
    public string? Search
    {
        get => _search;
        init => _search = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Tên field sort (ví dụ: "createdAt", "username").</summary>
    public string? SortBy { get; init; }

    /// <summary>true = ASC, false = DESC. Default DESC.</summary>
    public bool SortAscending { get; init; } = false;

    /// <summary>Số item cần skip (dùng trong repository).</summary>
    public int Skip => (PageNumber - 1) * PageSize;
}