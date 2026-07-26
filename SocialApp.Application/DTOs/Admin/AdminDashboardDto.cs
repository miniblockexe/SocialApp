namespace SocialApp.Application.DTOs.Admin;

/// <summary>
/// Tổng quan thống kê hệ thống dành cho admin dashboard.
/// Được cache 5 phút — trường GeneratedAt cho biết thời điểm query thực.
/// </summary>
public sealed class AdminDashboardDto
{
    // Users

    /// <summary>Tổng số tài khoản trong hệ thống.</summary>
    public int TotalUsers { get; init; }

    /// <summary>Số user có LastSeen trong 7 ngày gần nhất (UTC).</summary>
    public int ActiveUsersLast7Days { get; init; }

    /// <summary>Số user đăng ký hôm nay (theo ngày UTC).</summary>
    public int NewUsersToday { get; init; }

    /// <summary>Số tài khoản đang bị cấm (IsBanned = true).</summary>
    public int BannedUsers { get; init; }

    // Posts

    /// <summary>Tổng số bài đăng — bao gồm cả đã xóa mềm.</summary>
    public int TotalPosts { get; init; }

    /// <summary>Số bài đăng đang hoạt động (IsDeleted = false).</summary>
    public int ActivePosts { get; init; }

    /// <summary>Số bài đăng đã bị xóa (IsDeleted = true).</summary>
    public int DeletedPosts { get; init; }

    /// <summary>Số bài đăng được tạo hôm nay (theo ngày UTC).</summary>
    public int PostsToday { get; init; }

    // Messages & Comments & Likes

    /// <summary>Tổng số tin nhắn trong hệ thống.</summary>
    public int TotalMessages { get; init; }

    /// <summary>Số tin nhắn được gửi hôm nay (theo ngày UTC).</summary>
    public int MessagesToday { get; init; }

    /// <summary>Tổng số bình luận (bao gồm đã xóa).</summary>
    public int TotalComments { get; init; }

    /// <summary>Tổng số lượt thích.</summary>
    public int TotalLikes { get; init; }

    // Social

    /// <summary>Tổng số cặp bạn bè (FriendRequest với Status = Accepted).</summary>
    public int TotalFriendships { get; init; }

    // Meta

    /// <summary>Thời điểm query được thực thi (UTC). Dùng để biết cache có còn fresh không.</summary>
    public DateTime GeneratedAt { get; init; }
}
