using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Users;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý profile, avatar, cover photo và tìm kiếm người dùng.
/// Toàn bộ endpoint yêu cầu đã đăng nhập — [Authorize] đặt ở cấp controller.
/// </summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IValidator<UpdateProfileDto> _updateProfileValidator;
    private readonly ILogger<UsersController> _logger;

    private const long AvatarMaxRequestBytes = 5 * 1024 * 1024;
    private const long CoverMaxRequestBytes = 10 * 1024 * 1024;

    public UsersController(
        IUserService userService,
        IValidator<UpdateProfileDto> updateProfileValidator,
        ILogger<UsersController> logger)
    {
        _userService = userService;
        _updateProfileValidator = updateProfileValidator;
        _logger = logger;
    }



    /// <summary>Lấy profile của chính user đang đăng nhập.</summary>
    /// <response code="200">Trả về profile của user hiện tại.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Tài khoản không tồn tại (đã bị xóa).</response>
    [HttpGet("me")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile()
    {
        var userId = User.GetUserIdOrThrow();

        try
        {
            var profile = await _userService.GetMyProfileAsync(userId);
            return Ok(ApiResponse<UserProfileDto>.Ok(profile));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.NotFound(ex.Message));
        }
    }



    /// <summary>
    /// Lấy profile của một user bất kỳ theo góc nhìn của viewer đang đăng nhập.
    /// Nếu id trùng chính viewer → tự chuyển sang GetMyProfileAsync.
    /// </summary>
    /// <param name="id">Id của user cần xem.</param>
    /// <response code="200">Trả về profile.</response>
    /// <response code="400">id không hợp lệ (Guid.Empty).</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Không tồn tại.</response>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        var viewerId = User.GetUserIdOrThrow();

        try
        {
            var profile = id == viewerId
                ? await _userService.GetMyProfileAsync(viewerId)
                : await _userService.GetProfileAsync(id, viewerId);

            return Ok(ApiResponse<UserProfileDto>.Ok(profile));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.NotFound(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<UserProfileDto>.BadRequest(ex.Message));
        }
    }



    /// <summary>Cập nhật FullName / Bio của chính user đang đăng nhập.</summary>
    /// <param name="dto">Field nào có giá trị mới được cập nhật, field null giữ nguyên.</param>
    /// <response code="200">Cập nhật thành công — trả về profile mới.</response>
    /// <response code="400">Body rỗng hoặc null.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Tài khoản không tồn tại.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ (validation errors).</response>
    [HttpPut("me")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<UserProfileDto>.BadRequest("Body không được để trống."));

        var validation = await _updateProfileValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<UserProfileDto>.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            var profile = await _userService.UpdateProfileAsync(userId, dto);

            _logger.LogInformation(
                "[PUT /api/users/me] Cập nhật profile thành công — UserId: {UserId}", userId);

            return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Cập nhật thông tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<UserProfileDto>.NotFound(ex.Message));
        }
    }



    /// <summary>Upload/cập nhật ảnh đại diện (tối đa 5MB — JPEG/PNG/GIF/WEBP).</summary>
    /// <param name="file">File ảnh gửi qua multipart/form-data, field "file".</param>
    /// <response code="200">Cập nhật thành công — trả về URL ảnh mới.</response>
    /// <response code="400">File rỗng, null, hoặc sai định dạng.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Tài khoản không tồn tại.</response>
    /// <response code="413">File vượt quá 5MB.</response>
    [HttpPut("me/avatar")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(AvatarMaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UpdateAvatar([FromForm] FileUploadRequest request)
    {
        if (request.File is null)
            return BadRequest(ApiResponse<string>.BadRequest("Avatar không được để trống."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var url = await _userService.UpdateAvatarAsync(userId, request.File);

            _logger.LogInformation(
                "[PUT /api/users/me/avatar] Cập nhật avatar thành công — UserId: {UserId}", userId);

            return Ok(ApiResponse<string>.Ok(url, "Cập nhật ảnh đại diện thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.NotFound(ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<string>.PayloadTooLarge(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.BadRequest(ex.Message));
        }
    }



    /// <summary>Upload/cập nhật ảnh bìa (tối đa 10MB — JPEG/PNG/GIF/WEBP).</summary>
    /// <param name="file">File ảnh gửi qua multipart/form-data, field "file".</param>
    /// <response code="200">Cập nhật thành công — trả về URL ảnh mới.</response>
    /// <response code="400">File rỗng, null, hoặc sai định dạng.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Tài khoản không tồn tại.</response>
    /// <response code="413">File vượt quá 10MB.</response>
    [HttpPut("me/cover")]
    [EnableRateLimiting("upload")]
    [RequestSizeLimit(CoverMaxRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> UpdateCover([FromForm] FileUploadRequest request)
    {
        if (request.File is null)
            return BadRequest(ApiResponse<string>.BadRequest("Ảnh bìa không được để trống."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var url = await _userService.UpdateCoverAsync(userId, request.File);

            _logger.LogInformation(
                "[PUT /api/users/me/cover] Cập nhật cover thành công — UserId: {UserId}", userId);

            return Ok(ApiResponse<string>.Ok(url, "Cập nhật ảnh bìa thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.NotFound(ex.Message));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<string>.PayloadTooLarge(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<string>.BadRequest(ex.Message));
        }
    }



    /// <summary>Tìm kiếm người dùng theo username hoặc tên hiển thị.</summary>
    /// <param name="q">Từ khóa tìm kiếm — tối thiểu 2 ký tự.</param>
    /// <param name="page">Trang hiện tại (mặc định 1).</param>
    /// <param name="size">Số kết quả mỗi trang (mặc định 10, tối đa 100).</param>
    /// <response code="200">Danh sách kết quả phân trang, nhiều bạn chung hơn lên trước.</response>
    /// <response code="400">Từ khóa rỗng hoặc ngắn hơn 2 ký tự.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    [HttpGet("search")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserSearchResultDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? q, [FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var viewerId = User.GetUserIdOrThrow();

        try
        {
            var result = await _userService.SearchUsersAsync(viewerId, q ?? string.Empty, page, size);
            return Ok(ApiResponse<PagedResult<UserSearchResultDto>>.Ok(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<PagedResult<UserSearchResultDto>>.BadRequest(ex.Message));
        }
    }
}