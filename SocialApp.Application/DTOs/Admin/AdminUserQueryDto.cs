using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Query params để admin lọc và phân trang danh sách user.
/// Bind từ query string — defensive: page/size tự clamp, SortBy validate trong service.
/// </summary>
public sealed class AdminUserQueryDto
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
    /// Lọc theo trạng thái ban:
    /// null = tất cả, true = chỉ user bị ban, false = chỉ user không bị ban.
    /// </summary>
    public bool? IsBanned { get; init; }

    /// <summary>Lọc theo vai trò — null = tất cả role.</summary>
    public UserRole? Role { get; init; }

    /// <summary>
    /// Tìm kiếm theo Email hoặc Username (case-insensitive) — null = không lọc.
    /// </summary>
    public string? Keyword { get; init; }

    /// <summary>
    /// Field để sort. Giá trị hợp lệ: "createdAt" | "lastSeen" | "postCount".
    /// Giá trị không hợp lệ → service tự fallback về "createdAt".
    /// </summary>
    public string SortBy { get; init; } = "createdAt";

    /// <summary>true = giảm dần (mới nhất trước), false = tăng dần. Default true.</summary>
    public bool SortDesc { get; init; } = true;
}