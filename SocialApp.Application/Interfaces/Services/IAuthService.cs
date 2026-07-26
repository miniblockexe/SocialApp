using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Contract cho Authentication Service.
/// Xử lý đăng ký, đăng nhập, refresh token, thu hồi token và đổi mật khẩu.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Đăng ký tài khoản mới.
    /// </summary>
    /// <param name="dto">Thông tin đăng ký (username, email, password, fullName).</param>
    /// <returns>Cặp token và thông tin user vừa tạo.</returns>
    /// <exception cref="InvalidOperationException">409 — Email hoặc username đã tồn tại.</exception>
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto);

    /// <summary>
    /// Đăng nhập bằng email và mật khẩu.
    /// Có rate limiting: 5 lần sai / 15 phút per IP → 429.
    /// </summary>
    /// <param name="dto">Email và mật khẩu.</param>
    /// <param name="ipAddress">IP của client — dùng để rate limit login attempt.</param>
    /// <returns>Cặp token và thông tin user.</returns>
    /// <exception cref="InvalidOperationException">429 — Quá nhiều lần thử đăng nhập.</exception>
    /// <exception cref="UnauthorizedAccessException">401 — Email hoặc mật khẩu không đúng.</exception>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, string ipAddress);

    /// <summary>
    /// Làm mới cặp token (token rotation).
    /// Phát hiện replay attack (token đã revoke bị dùng lại) → revoke toàn bộ session.
    /// </summary>
    /// <param name="refreshToken">Refresh token hiện tại.</param>
    /// <returns>Cặp token mới.</returns>
    /// <exception cref="UnauthorizedAccessException">401 — Token không hợp lệ, đã revoke hoặc hết hạn.</exception>
    /// <exception cref="InvalidOperationException">403 — Tài khoản bị ban.</exception>
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Thu hồi một refresh token cụ thể (đăng xuất khỏi một thiết bị).
    /// </summary>
    /// <param name="refreshToken">Refresh token cần thu hồi.</param>
    /// <param name="userId">ID của user đang thực hiện yêu cầu (từ JWT claim).</param>
    /// <exception cref="KeyNotFoundException">404 — Token không tồn tại.</exception>
    /// <exception cref="UnauthorizedAccessException">403 — Token không thuộc về user này.</exception>
    /// <exception cref="InvalidOperationException">400 — Token đã được thu hồi trước đó.</exception>
    Task RevokeTokenAsync(string refreshToken, Guid userId);

    /// <summary>
    /// Đổi mật khẩu. Revoke toàn bộ refresh token để force logout mọi thiết bị.
    /// </summary>
    /// <param name="userId">ID của user đang thực hiện yêu cầu (từ JWT claim).</param>
    /// <param name="dto">Mật khẩu cũ, mật khẩu mới và xác nhận mật khẩu mới.</param>
    /// <exception cref="KeyNotFoundException">404 — User không tồn tại.</exception>
    /// <exception cref="InvalidOperationException">400 — Mật khẩu cũ sai hoặc mật khẩu mới trùng cũ.</exception>
    Task ChangePasswordAsync(Guid userId, ChangePasswordDto dto);

    /// <summary>
    /// Tạo JWT access token cho user.
    /// Claims: sub, email, preferred_username, role, jti.
    /// </summary>
    /// <param name="user">Entity User đầy đủ thông tin.</param>
    /// <returns>JWT token string đã ký bằng HS256.</returns>
    string GenerateAccessToken(User user);

    /// <summary>
    /// Tạo refresh token mới (chưa lưu DB).
    /// Token = 64 random bytes → Base64. ExpiresAt = UtcNow + RefreshTokenExpiryDays.
    /// </summary>
    /// <returns>Entity <see cref="RefreshToken"/> chưa có UserId — caller phải gán trước khi lưu.</returns>
    RefreshToken GenerateRefreshToken();
}