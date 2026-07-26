using SocialApp.Application.DTOs.Auth;

namespace SocialApp.Application.DTOs.Friends;

/// <summary>
/// DTO đại diện cho một gợi ý kết bạn.
/// Dùng cho endpoint GET /api/friends/suggestions.
/// </summary>
public sealed class FriendSuggestionDto
{
    /// <summary>Thông tin cơ bản của user được gợi ý.</summary>
    public UserBriefDto User { get; init; } = null!;

    /// <summary>Số bạn chung giữa viewer và user được gợi ý.</summary>
    public int MutualFriendsCount { get; init; }

    /// <summary>
    /// Preview tối đa 3 người bạn chung — hiển thị avatar/tên để tăng độ tin cậy gợi ý.
    /// </summary>
    public List<UserBriefDto> MutualFriends { get; init; } = [];
}