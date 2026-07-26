using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Friends;

/// <summary>
/// DTO response trả về sau các thao tác liên quan đến friend request:
/// gửi lời mời, chấp nhận, từ chối, lấy danh sách pending/sent.
/// </summary>
public sealed class FriendResponseDto
{
    /// <summary>Id của FriendRequest record.</summary>
    public Guid RequestId { get; init; }

    /// <summary>Trạng thái hiện tại của quan hệ.</summary>
    public FriendStatus Status { get; init; }

    /// <summary>User đã gửi lời mời kết bạn.</summary>
    public UserBriefDto Sender { get; init; } = null!;

    /// <summary>User nhận lời mời kết bạn.</summary>
    public UserBriefDto Receiver { get; init; } = null!;

    /// <summary>Thời điểm tạo record (UTC).</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Thời điểm cập nhật trạng thái gần nhất (UTC).</summary>
    public DateTime UpdatedAt { get; init; }
}