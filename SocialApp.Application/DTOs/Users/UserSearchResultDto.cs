namespace SocialApp.Application.DTOs.Users;

/// <summary>
/// DTO trả về khi tìm kiếm người dùng.
/// MutualFriendsCount và FriendshipStatus được tính thủ công trong service,
/// không map qua AutoMapper.
/// </summary>
public sealed class UserSearchResultDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }

    /// <summary>Số bạn chung giữa viewer và user này.</summary>
    public int MutualFriendsCount { get; set; }

    /// <summary>Trạng thái quan hệ giữa viewer và user này.</summary>
    public FriendshipStatus FriendshipStatus { get; set; }
}