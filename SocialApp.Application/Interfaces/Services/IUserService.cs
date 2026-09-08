using Microsoft.AspNetCore.Http;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Users;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Contract cho User Service.
/// Xử lý profile, avatar, cover photo và tìm kiếm người dùng.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Lấy profile đầy đủ của một user bất kỳ (theo góc nhìn của viewer).
    /// Tính FriendshipStatus, FriendCount, PostCount.
    /// Nếu viewer không đủ quyền xem (theo ProfileVisibility của target) — KHÔNG throw,
    /// vẫn trả 200 với Id/Username/FullName/AvatarUrl để FE render header, nhưng
    /// IsRestricted = true và Bio/CoverPhotoUrl/RingtoneUrl/FriendCount/PostCount bị ẩn.
    /// </summary>
    /// <param name="targetId">Id của user cần xem profile.</param>
    /// <param name="viewerId">Id của user đang thực hiện request.</param>
    /// <returns>UserProfileDto — có thể là bản đầy đủ hoặc bản bị hạn chế (IsRestricted = true).</returns>
    /// <exception cref="KeyNotFoundException">404 — targetId không tồn tại.</exception>
    Task<UserProfileDto> GetProfileAsync(Guid targetId, Guid viewerId);

    /// <summary>Lấy cài đặt riêng tư hiện tại của user đang đăng nhập.</summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    Task<PrivacySettingsDto> GetPrivacySettingsAsync(Guid userId);

    /// <summary>Cập nhật cài đặt riêng tư của user đang đăng nhập (ghi đè toàn bộ 4 field).</summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <param name="dto">Giá trị mới cho cả 4 field.</param>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    Task<PrivacySettingsDto> UpdatePrivacySettingsAsync(Guid userId, PrivacySettingsDto dto);

    /// <summary>
    /// Lấy profile của chính user đang đăng nhập.
    /// FriendshipStatus luôn là None (tự xem profile mình).
    /// </summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <returns>UserProfileDto của user hiện tại.</returns>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    Task<UserProfileDto> GetMyProfileAsync(Guid userId);

    /// <summary>
    /// Cập nhật thông tin profile (FullName, Bio).
    /// Chỉ update field có giá trị, bỏ qua field null.
    /// </summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <param name="dto">Thông tin cần cập nhật.</param>
    /// <returns>UserProfileDto sau khi cập nhật.</returns>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    Task<UserProfileDto> UpdateProfileAsync(Guid userId, UpdateProfileDto dto);

    /// <summary>
    /// Upload và cập nhật ảnh đại diện.
    /// Validate magic bytes, xóa ảnh cũ trên Cloudinary (bất đồng bộ, không block).
    /// </summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <param name="file">File ảnh upload (JPEG/PNG/GIF/WEBP, tối đa 5MB).</param>
    /// <returns>URL mới của ảnh đại diện.</returns>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    /// <exception cref="ArgumentException">400 — file không hợp lệ (null, 0 byte, sai format, quá size).</exception>
    Task<string> UpdateAvatarAsync(Guid userId, IFormFile file);

    /// <summary>
    /// Upload và cập nhật ảnh bìa.
    /// Validate magic bytes, xóa ảnh cũ trên Cloudinary (bất đồng bộ, không block).
    /// </summary>
    /// <param name="userId">Id của user đang đăng nhập.</param>
    /// <param name="file">File ảnh upload (JPEG/PNG/GIF/WEBP, tối đa 10MB).</param>
    /// <returns>URL mới của ảnh bìa.</returns>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    /// <exception cref="ArgumentException">400 — file không hợp lệ (null, 0 byte, sai format, quá size).</exception>
    Task<string> UpdateCoverAsync(Guid userId, IFormFile file);

    /// <summary>
    /// Tìm kiếm người dùng theo username hoặc tên hiển thị.
    /// Loại trừ chính viewer, loại trừ user đã block/bị block.
    /// Sắp xếp: nhiều bạn chung lên trước.
    /// </summary>
    /// <param name="viewerId">Id của user đang tìm kiếm.</param>
    /// <param name="keyword">Từ khóa — tối thiểu 2 ký tự.</param>
    /// <param name="page">Trang hiện tại (mặc định 1).</param>
    /// <param name="size">Số kết quả mỗi trang (mặc định 10, tối đa 100).</param>
    /// <returns>Danh sách kết quả phân trang.</returns>
    /// <exception cref="ArgumentException">400 — keyword ngắn hơn 2 ký tự.</exception>
    Task<PagedResult<UserSearchResultDto>> SearchUsersAsync(
        Guid viewerId, string keyword, int page, int size);

    /// <summary>
    /// Lấy thông tin tóm tắt của user (dùng cho embed trong các DTO khác).
    /// </summary>
    /// <param name="userId">Id của user cần lấy.</param>
    /// <returns>UserBriefDto.</returns>
    /// <exception cref="KeyNotFoundException">404 — userId không tồn tại.</exception>
    Task<UserBriefDto> GetUserBriefAsync(Guid userId);
    Task<string> UpdateRingtoneAsync(Guid userId, IFormFile file);
    Task DeleteRingtoneAsync(Guid userId);
}