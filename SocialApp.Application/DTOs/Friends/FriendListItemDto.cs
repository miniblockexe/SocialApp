using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Friends;

/// <summary>
/// DTO đại diện cho một người bạn trong danh sách bạn bè của user.
/// Dùng cho endpoint GET /api/friends.
/// </summary>
public sealed class FriendListItemDto
{
    /// <summary>Thông tin cơ bản của người bạn.</summary>
    public UserBriefDto User { get; init; } = null!;

    /// <summary>Thời điểm hai người trở thành bạn bè (UTC) — lấy từ UpdatedAt của FriendRequest.</summary>
    public DateTime FriendSince { get; init; }

    /// <summary>Số bạn chung giữa viewer và người bạn này.</summary>
    public int MutualFriendsCount { get; init; }
}