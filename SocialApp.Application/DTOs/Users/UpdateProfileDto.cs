namespace SocialApp.Application.DTOs.Users;

/// <summary>
/// DTO cập nhật thông tin cá nhân.
/// Cả 2 field đều nullable — chỉ update field nào client gửi lên (có giá trị).
/// Service sẽ Trim() và bỏ qua nếu null hoặc whitespace.
/// </summary>
public sealed class UpdateProfileDto
{
    /// <summary>
    /// Tên hiển thị — nullable, tối đa 100 ký tự.
    /// Nếu null → không update FullName.
    /// </summary>
    public string? FullName { get; init; }

    /// <summary>
    /// Giới thiệu bản thân — nullable, tối đa 500 ký tự.
    /// Nếu null → không update Bio.
    /// Nếu empty string → xóa Bio (set về null).
    /// </summary>
    public string? Bio { get; init; }
}