namespace SocialApp.Domain.Enums;

/// <summary>Loại thông báo trong hệ thống.</summary>
public enum NotificationType
{
    Like = 0,
    Comment = 1,
    FriendRequest = 2,
    FriendAccepted = 3,
    Message = 4,
    System = 5
}