namespace SocialApp.Application.DTOs.Users;

/// <summary>
/// Trạng thái quan hệ bạn bè giữa viewer và target user.
/// </summary>
public enum FriendshipStatus
{
    /// <summary>Chưa có quan hệ.</summary>
    None = 0,

    /// <summary>Target đã gửi friend request cho viewer (viewer đang nhận lời mời).</summary>
    Pending = 1,

    /// <summary>Viewer đã gửi friend request cho target (đang chờ xác nhận).</summary>
    SentRequest = 2,

    /// <summary>Hai người đã là bạn bè.</summary>
    Friends = 3,

    /// <summary>Bị block (bất kỳ chiều nào).</summary>
    Blocked = 4
}