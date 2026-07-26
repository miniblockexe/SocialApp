namespace SocialApp.Domain.Enums;

/// <summary>Trạng thái quan hệ bạn bè giữa 2 user.</summary>
public enum FriendStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Blocked = 3
}