namespace SocialApp.Application.DTOs.Users;

/// <summary>
/// DTO đầy đủ thông tin profile người dùng — trả về khi xem trang cá nhân.
/// FriendCount, PostCount, FriendshipStatus được tính thủ công trong service,
/// không map qua AutoMapper.
/// </summary>
public sealed class UserProfileDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    /// <summary>Set (không phải init) vì service cần xóa giá trị khi profile bị hạn chế xem.</summary>
    public string? Bio { get; set; }
    public string? AvatarUrl { get; init; }
    /// <summary>Set (không phải init) vì service cần xóa giá trị khi profile bị hạn chế xem.</summary>
    public string? CoverPhotoUrl { get; set; }
    /// <summary>URL nhạc chuông tuỳ chỉnh (R2). Null = dùng nhạc chuông mặc định.
    /// Set (không phải init) vì service cần xóa giá trị khi profile bị hạn chế xem.</summary>
    public string? RingtoneUrl { get; set; }

    /// <summary>Thời điểm tạo tài khoản (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Số bạn bè hiện tại — tính từ FriendRequest status=Accepted.</summary>
    public int FriendCount { get; set; }

    /// <summary>Số bài đăng chưa xóa.</summary>
    public int PostCount { get; set; }

    /// <summary>Trạng thái quan hệ giữa viewer và user này.</summary>
    public FriendshipStatus FriendshipStatus { get; set; }

    /// <summary>
    /// True nếu viewer KHÔNG đủ quyền xem chi tiết trang cá nhân này (theo
    /// ProfileVisibility mà chủ tài khoản đã cấu hình). Khi true, các field
    /// Bio, CoverPhotoUrl, RingtoneUrl, FriendCount, PostCount đã bị ẩn (trả về
    /// giá trị rỗng/0) — FE nên hiển thị thông báo thay vì gọi tiếp API bài viết/bạn bè.
    /// Id, Username, FullName, AvatarUrl vẫn luôn hiển thị để FE render phần header.
    /// </summary>
    public bool IsRestricted { get; set; }
}