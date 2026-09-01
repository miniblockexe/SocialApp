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

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IValidator<RegisterRequestDto> _registerValidator;
    private readonly IValidator<LoginRequestDto> _loginValidator;
    private readonly IValidator<ChangePasswordDto> _changePasswordValidator;
    private readonly IValidator<ForgotPasswordDto> _forgotPasswordValidator;
    private readonly IValidator<ResetPasswordDto> _resetPasswordValidator;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IValidator<RegisterRequestDto> registerValidator,
        IValidator<LoginRequestDto> loginValidator,
        IValidator<ChangePasswordDto> changePasswordValidator,
        IValidator<ForgotPasswordDto> forgotPasswordValidator,
        IValidator<ResetPasswordDto> resetPasswordValidator,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _changePasswordValidator = changePasswordValidator;
        _forgotPasswordValidator = forgotPasswordValidator;
        _resetPasswordValidator = resetPasswordValidator;
        _logger = logger;
    }

    /// <summary>Đăng ký tài khoản mới.</summary>
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
            return StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse<AuthResponseDto>.TooManyRequests(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Unauthorized(ex.Message));
        }
    }

    /// <summary>Đăng nhập bằng Google ID Token — tự động tạo tài khoản nếu chưa có.</summary>
    [HttpPost("google-login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto? dto)
    {
        if (dto is null || string.IsNullOrWhiteSpace(dto.IdToken))
            return BadRequest(ApiResponse<AuthResponseDto>.BadRequest("IdToken không được để trống."));

        var ipAddress = GetClientIpAddress();

        try
        {
            var result = await _authService.GoogleLoginAsync(dto.IdToken, ipAddress);

            _logger.LogInformation(
                "[POST /api/auth/google-login] Đăng nhập Google thành công — UserId: {UserId}, thời gian: {Time:O}",
                result.User.Id, DateTime.UtcNow);

            return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Đăng nhập Google thành công."));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<AuthResponseDto>.Forbidden(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Unauthorized(ex.Message));
        }
    }

    /// <summary>Gửi OTP reset mật khẩu qua email — luôn 204 để tránh user enumeration.</summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto? dto)
    {
        if (dto is null) return BadRequest(ApiResponse.BadRequest("Body không được để trống."));

        var validation = await _forgotPasswordValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse.UnprocessableEntity(
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        await _authService.ForgotPasswordAsync(dto.Email);
        return NoContent();
    }

    [HttpPost("verify-otp")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto? dto)
    {
        if (dto is null) return BadRequest(ApiResponse.BadRequest("Body không được để trống."));

        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Token))
            return BadRequest(ApiResponse.BadRequest("Email và OTP không được để trống."));

        try
        {
            var result = await _authService.VerifyOtpAsync(dto.Email, dto.Token);
            return Ok(ApiResponse<VerifyOtpResponseDto>.Ok(result, "OTP hợp lệ."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.BadRequest(ex.Message));
        }
    }

    /// <summary>Xác thực OTP và đặt mật khẩu mới.</summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto? dto)
    {
        if (dto is null) return BadRequest(ApiResponse.BadRequest("Body không được để trống."));

        var validation = await _resetPasswordValidator.ValidateAsync(dto);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse.UnprocessableEntity(
                validation.Errors.Select(e => e.ErrorMessage).ToList()));

        try
        {
            await _authService.ResetPasswordAsync(dto);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(ApiResponse.BadRequest(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse.BadRequest(ex.Message)); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse.NotFound(ex.Message)); }
    }

    /// <summary>Làm mới cặp token (token rotation).</summary>
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
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<AuthResponseDto>.Forbidden(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Unauthorized(ex.Message));
        }
    }

    /// <summary>Thu hồi một refresh token (đăng xuất khỏi một thiết bị).</summary>
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

    /// <summary>Đổi mật khẩu và force logout toàn bộ thiết bị.</summary>
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

    private string GetClientIpAddress()
    {
        var forwarded = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();

        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
    }
}