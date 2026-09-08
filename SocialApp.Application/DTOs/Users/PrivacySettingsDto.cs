using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Users;

/// <summary>
/// DTO cấu hình quyền riêng tư của user — dùng chung cho cả GET và PUT
/// /api/users/me/privacy-settings.
/// Mỗi field dùng enum PostPrivacy (Public / Friends / OnlyMe) để tái sử dụng,
/// không tạo enum riêng.
/// </summary>
public sealed class PrivacySettingsDto
{
    /// <summary>Ai được xem trang cá nhân (thông tin chi tiết) của user.</summary>
    public PostPrivacy ProfileVisibility { get; init; }

    /// <summary>
    /// Ai được xem bài đăng của user — lớp lọc bổ sung, cộng thêm vào Privacy
    /// của từng bài viết riêng lẻ.
    /// </summary>
    public PostPrivacy PostVisibility { get; init; }

    /// <summary>Ai được xem danh sách bạn bè của user.</summary>
    public PostPrivacy FriendListVisible { get; init; }

    /// <summary>Ai được tìm thấy user qua chức năng tìm kiếm.</summary>
    public PostPrivacy SearchDiscoverable { get; init; }
}