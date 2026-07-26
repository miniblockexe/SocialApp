using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Xử lý đăng ký, đăng nhập, làm mới token, thu hồi token và đổi mật khẩu.
/// Tất cả response đều theo chuẩn ApiResponse&lt;T&gt;.
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _logger = logger;
    }



    /// <summary>Đăng ký tài khoản mới.</summary>
    /// <param name="dto">Thông tin đăng ký: username, email, password, fullName.</param>
    /// <response code="201">Đăng ký thành công — trả về cặp token và thông tin user.</response>
    /// <response code="400">Body rỗng hoặc null.</response>
    /// <response code="409">Email hoặc username đã được sử dụng.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ (validation errors).</response>
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<AuthResponseDto>.BadRequest("Body không được để trống."));

        var validation = await _registerValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<AuthResponseDto>.UnprocessableEntity(errors));
        }

        try
        {
            var result = await _authService.RegisterAsync(dto);

            _logger.LogInformation(
                "[POST /api/auth/register] Đăng ký thành công — UserId: {UserId}, thời gian: {Time:O}",
                result.User.Id, DateTime.UtcNow);

            // 201 Created — không có Location header vì UserController chưa tồn tại
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<AuthResponseDto>.Created(result, "Đăng ký thành công."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "EMAIL_EXISTED")
        {
            return Conflict(ApiResponse<AuthResponseDto>.Conflict("Email đã được sử dụng."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "USERNAME_EXISTED")
        {
            return Conflict(ApiResponse<AuthResponseDto>.Conflict("Username đã được sử dụng."));
        }
    }



    /// <summary>Đăng nhập bằng email và mật khẩu.</summary>
    /// <param name="dto">Email và mật khẩu.</param>
    /// <response code="200">Đăng nhập thành công — trả về cặp token và thông tin user.</response>
    /// <response code="400">Body rỗng hoặc null.</response>
    /// <response code="401">Email hoặc mật khẩu không đúng.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="429">Quá nhiều lần đăng nhập thất bại từ IP này — thử lại sau.</response>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<AuthResponseDto>.BadRequest("Body không được để trống."));

        var validation = await _loginValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<AuthResponseDto>.UnprocessableEntity(errors));
        }

        var ipAddress = GetClientIpAddress();

        try
        {
            var result = await _authService.LoginAsync(dto, ipAddress);

            _logger.LogInformation(
                "[POST /api/auth/login] Đăng nhập thành công — UserId: {UserId}, IP: {IP}, thời gian: {Time:O}",
                result.User.Id, ipAddress, DateTime.UtcNow);

            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập thành công."));
        }
        catch (TooManyRequestsException ex)
        {
            // Giữ nguyên ex.Message vì service đã tính thời gian còn lại cụ thể
            return StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse<AuthResponseDto>.TooManyRequests(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            // Không dùng GlobalExceptionMiddleware vì nó override thành message chung;
            // ở đây cần giữ "Email hoặc mật khẩu không đúng." để trả đúng spec
            return Unauthorized(ApiResponse<AuthResponseDto>.Unauthorized(ex.Message));
        }
    }



    /// <summary>Làm mới cặp access token và refresh token (token rotation).</summary>
    /// <param name="dto">Refresh token hiện tại.</param>
    /// <response code="200">Token mới đã được tạo và token cũ đã bị thu hồi.</response>
    /// <response code="400">Body rỗng hoặc refresh token trống.</response>
    /// <response code="401">Token không hợp lệ, đã revoke, hoặc đã hết hạn.</response>
    /// <response code="403">Tài khoản đã bị khóa.</response>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest(ApiResponse<AuthResponseDto>.BadRequest("Refresh token không được để trống."));

        try
        {
            var result = await _authService.RefreshTokenAsync(dto.RefreshToken.Trim());
            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Làm mới token thành công."));
        }
        catch (ForbiddenException ex)
        {
            // ForbiddenException không được GlobalExceptionMiddleware handle → catch tại đây
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<AuthResponseDto>.Forbidden(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            // Giữ message cụ thể: "Token không hợp lệ.", "Token đã bị thu hồi...", "Token đã hết hạn."
            return Unauthorized(ApiResponse<AuthResponseDto>.Unauthorized(ex.Message));
        }
    }



    /// <summary>Thu hồi một refresh token (đăng xuất khỏi một thiết bị cụ thể).</summary>
    /// <param name="dto">Refresh token cần thu hồi.</param>
    /// <response code="204">Token đã được thu hồi thành công.</response>
    /// <response code="400">Token đã được thu hồi trước đó hoặc body không hợp lệ.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Token không thuộc về người dùng này.</response>
    /// <response code="404">Token không tồn tại trong hệ thống.</response>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequestDto? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.RefreshToken))
            return BadRequest(ApiResponse.BadRequest("Refresh token không được để trống."));

        // [Authorize] đảm bảo user đã xác thực; GetUserIdOrThrow throw nếu claim sub thiếu
        var userId = User.GetUserIdOrThrow();

        try
        {
            await _authService.RevokeTokenAsync(dto.RefreshToken.Trim(), userId);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.BadRequest(ex.Message));
        }
    }



    /// <summary>Đổi mật khẩu và force logout toàn bộ thiết bị đang đăng nhập.</summary>
    /// <param name="dto">Mật khẩu cũ, mật khẩu mới và xác nhận mật khẩu mới.</param>
    /// <response code="204">Đổi mật khẩu thành công, toàn bộ phiên đăng nhập đã bị thu hồi.</response>
    /// <response code="400">Mật khẩu cũ sai hoặc mật khẩu mới trùng mật khẩu cũ.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Tài khoản không tồn tại.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ (validation errors).</response>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse.BadRequest("Body không được để trống."));

        var validation = await _changePasswordValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _authService.ChangePasswordAsync(userId, dto);

            _logger.LogInformation(
                "[PUT /api/auth/change-password] Đổi mật khẩu thành công — UserId: {UserId}, thời gian: {Time:O}",
                userId, DateTime.UtcNow);

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex) when (ex.Message == "OLD_PASSWORD_WRONG")
        {
            return BadRequest(ApiResponse.BadRequest("Mật khẩu cũ không đúng."));
        }
        catch (InvalidOperationException ex) when (ex.Message == "NEW_PASSWORD_SAME_AS_OLD")
        {
            return BadRequest(ApiResponse.BadRequest("Mật khẩu mới không được trùng mật khẩu cũ."));
        }
    }



    /// <summary>
    /// Lấy IP thực của client.
    /// Ưu tiên header X-Forwarded-For (khi có reverse proxy / load balancer / CDN).
    /// Fallback về RemoteIpAddress nếu kết nối trực tiếp.
    /// </summary>
    private string GetClientIpAddress()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
    }
}