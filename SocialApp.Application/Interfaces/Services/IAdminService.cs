using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Admin;
using SocialApp.Application.DTOs.Cloud;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Interface cho toàn bộ business logic của Admin module.
/// Mọi action đều được audit log ở Warning level.
/// </summary>
public interface IAdminService
{
    /// <summary>
    /// Lấy tổng quan thống kê hệ thống.
    /// Kết quả được cache 5 phút (key: "admin:dashboard").
    /// Chạy song song tất cả query để tối ưu thời gian phản hồi.
    /// </summary>
    Task<AdminDashboardDto> GetDashboardStatsAsync();

    /// <summary>
    /// Lấy danh sách bài đăng (bao gồm cả đã xóa) với filter và phân trang.
    /// Admin có thể xem tất cả bài kể cả OnlyMe và đã xóa.
    /// </summary>
    Task<PagedResult<AdminPostDto>> GetAllPostsAsync(AdminPostQueryDto query);

    /// <summary>
    /// Soft delete bài đăng bởi admin, ghi lý do và audit log.
    /// KHÔNG xóa media trên cloud — giữ lại cho mục đích audit.
    /// Invalidate cache dashboard sau khi xóa.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Post không tồn tại → 404.</exception>
    /// <exception cref="InvalidOperationException">Post đã bị xóa trước đó → 400.</exception>
    Task AdminDeletePostAsync(Guid adminId, Guid postId, string reason);

    /// <summary>
    /// Khôi phục bài đăng đã bị xóa mềm.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Post không tồn tại → 404.</exception>
    /// <exception cref="InvalidOperationException">Post chưa bị xóa → 400.</exception>
    Task AdminRestorePostAsync(Guid adminId, Guid postId);

    /// <summary>
    /// Lấy danh sách user với filter và phân trang.
    /// KHÔNG trả PasswordHash trong bất kỳ trường hợp nào.
    /// </summary>
    Task<PagedResult<AdminUserDto>> GetAllUsersAsync(AdminUserQueryDto query);

    /// <summary>
    /// Cấm tài khoản user: set IsBanned, revoke toàn bộ refresh token,
    /// xóa cache BannedUser middleware, tạo system notification.
    /// </summary>
    /// <exception cref="ArgumentException">adminId == targetUserId → 400.</exception>
    /// <exception cref="KeyNotFoundException">User không tồn tại → 404.</exception>
    /// <exception cref="UnauthorizedAccessException">Target là Admin → 403.</exception>
    /// <exception cref="InvalidOperationException">User đã bị ban → 400.</exception>
    Task BanUserAsync(Guid adminId, Guid targetUserId, string reason);

    /// <summary>
    /// Gỡ lệnh cấm tài khoản user, tạo system notification thông báo cho user.
    /// </summary>
    /// <exception cref="KeyNotFoundException">User không tồn tại → 404.</exception>
    /// <exception cref="InvalidOperationException">User không bị ban → 400.</exception>
    Task UnbanUserAsync(Guid adminId, Guid targetUserId);

    /// <summary>
    /// Lấy thống kê cloud storage (Cloudinary + R2).
    /// Kết quả được cache 10 phút (key: "admin:cloud_stats").
    /// </summary>
    Task<AdminCloudStatsDto> GetCloudStatsAsync();

    /// <summary>
    /// Xóa file trực tiếp trên cloud storage.
    /// Nếu dto.PostMediaFileId có giá trị → xóa luôn record PostMediaFile trong DB.
    /// Invalidate cloud stats cache sau khi xóa.
    /// </summary>
    /// <exception cref="ArgumentException">PublicIdOrKey rỗng/whitespace → 400.</exception>
    Task AdminDeleteCloudFileAsync(Guid adminId, AdminDeleteCloudFileDto dto);
}