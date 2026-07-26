using SocialApp.Domain.Common;
using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

/// <summary>
/// Đại diện cho tài khoản người dùng trong hệ thống.
/// Kế thừa BaseAuditableEntity: Id (Guid), CreatedAt, UpdatedAt, DeletedAt, IsDeleted.
/// </summary>
public class User : BaseAuditableEntity
{
    /// <summary>Tên đăng nhập — unique, tối đa 50 ký tự.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Email — unique, lưu dạng lowercase.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Mật khẩu đã hash (BCrypt). KHÔNG bao giờ trả ra response.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Tên hiển thị — tối đa 100 ký tự.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Giới thiệu bản thân — tối đa 500 ký tự, nullable.</summary>
    public string? Bio { get; set; }

    /// <summary>URL ảnh đại diện (Cloudinary).</summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// PublicId của avatar trên Cloudinary — dùng để xóa file cũ khi upload mới.
    /// Ví dụ: "socialapp/avatars/abc123" (không phải URL đầy đủ).
    /// </summary>
    public string? AvatarPublicId { get; set; }

    /// <summary>URL ảnh bìa (Cloudinary).</summary>
    public string? CoverPhotoUrl { get; set; }

    /// <summary>
    /// PublicId của ảnh bìa trên Cloudinary — dùng để xóa file cũ khi upload mới.
    /// Ví dụ: "socialapp/covers/xyz789" (không phải URL đầy đủ).
    /// </summary>
    public string? CoverPublicId { get; set; }

    /// <summary>Vai trò: User = 0, Admin = 1.</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>Tài khoản đang hoạt động (chưa bị xoá / vô hiệu hoá).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Tài khoản đang bị ban.</summary>
    public bool IsBanned { get; set; } = false;

    /// <summary>Lý do bị ban — nullable khi chưa bị ban.</summary>
    public string? BannedReason { get; set; }

    /// <summary>Lần cuối online (UTC). Nullable khi user chưa từng đăng nhập.</summary>
    public DateTime? LastSeen { get; set; }

    // Navigation properties

    /// <summary>Các bài đăng của user.</summary>
    public ICollection<Post> Posts { get; set; } = new List<Post>();

    /// <summary>Các lời mời kết bạn mà user đã gửi đi.</summary>
    public ICollection<FriendRequest> SentFriendRequests { get; set; } = new List<FriendRequest>();

    /// <summary>Các lời mời kết bạn mà user nhận được.</summary>
    public ICollection<FriendRequest> ReceivedFriendRequests { get; set; } = new List<FriendRequest>();

    /// <summary>Danh sách tham gia hội thoại (qua bảng trung gian ConversationParticipant).</summary>
    public ICollection<ConversationParticipant> Conversations { get; set; } = new List<ConversationParticipant>();

    /// <summary>Các tin nhắn mà user đã gửi.</summary>
    public ICollection<Message> SentMessages { get; set; } = new List<Message>();

    /// <summary>Thông báo của user (là người nhận).</summary>
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    /// <summary>Refresh token của user.</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}