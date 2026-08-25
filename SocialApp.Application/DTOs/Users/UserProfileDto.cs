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
    public string? Bio { get; init; }
    public string? AvatarUrl { get; init; }
    public string? CoverPhotoUrl { get; init; }
    /// <summary>URL nhạc chuông tuỳ chỉnh (R2). Null = dùng nhạc chuông mặc định.</summary>
    public string? RingtoneUrl { get; init; }

    /// <summary>Thời điểm tạo tài khoản (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Số bạn bè hiện tại — tính từ FriendRequest status=Accepted.</summary>
    public int FriendCount { get; set; }

    /// <summary>Số bài đăng chưa xóa.</summary>
    public int PostCount { get; set; }

    /// <summary>Trạng thái quan hệ giữa viewer và user này.</summary>
    public FriendshipStatus FriendshipStatus { get; set; }
}