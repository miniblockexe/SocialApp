using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Thông tin chi tiết user dành cho admin — bao gồm trạng thái ban và thống kê hoạt động.
/// KHÔNG chứa PasswordHash hay thông tin nhạy cảm khác.
/// </summary>
public sealed class AdminUserDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;

    /// <summary>URL ảnh đại diện — null nếu chưa đặt.</summary>
    public string? AvatarUrl { get; init; }

    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public bool IsBanned { get; init; }

    /// <summary>Lý do bị ban — null nếu tài khoản không bị ban.</summary>
    public string? BannedReason { get; init; }

    /// <summary>Thời điểm tạo tài khoản (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Lần cuối online (UTC) — null nếu chưa từng đăng nhập.</summary>
    public DateTime? LastSeen { get; init; }

    // Thống kê hoạt động

    /// <summary>Số bài đăng chưa bị xóa (IsDeleted = false).</summary>
    public int PostCount { get; init; }

    /// <summary>Số bạn bè (FriendRequest status = Accepted liên quan đến user này).</summary>
    public int FriendCount { get; init; }

    /// <summary>Số tin nhắn user đã gửi.</summary>
    public int MessageCount { get; init; }
}