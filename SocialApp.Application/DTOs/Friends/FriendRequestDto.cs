namespace SocialApp.Application.DTOs.Friends;

/// <summary>
/// DTO cho endpoint POST /api/friends/request.
/// Chỉ cần ReceiverId — SenderId lấy từ JWT claim.
/// </summary>
public sealed class FriendRequestDto
{
    /// <summary>
    /// Id của user nhận lời mời kết bạn.
    /// Không được là Guid.Empty — validate bởi FriendRequestValidator.
    /// </summary>
    public Guid ReceiverId { get; init; }
}