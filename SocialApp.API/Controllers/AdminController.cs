using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Admin;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản trị hệ thống: dashboard, quản lý post/user, cloud storage.
/// Toàn bộ endpoint yêu cầu role Admin.
/// Mọi action được audit log ở Warning level trong service.
/// </summary>
[ApiController]
[Route("api/admin")]
[Produces("application/json")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting("default")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAdminService adminService,
        ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }



    /// <summary>
    /// Lấy tổng quan thống kê hệ thống.
    /// Kết quả được cache 5 phút — header X-Cache-Generated-At cho biết thời điểm query thực.
    /// </summary>
    /// <response code="200">AdminDashboardDto — tổng quan thống kê.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDashboard()
    {
        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.GetDashboard called. AdminId={AdminId}", adminId);

        var result = await _adminService.GetDashboardStatsAsync();

        Response.Headers["X-Cache-Generated-At"] =
            result.GeneratedAt.ToString("o"); // ISO 8601

        return Ok(ApiResponse<AdminDashboardDto>.Ok(result, "Thống kê hệ thống."));
    }



    /// <summary>
    /// Lấy danh sách bài đăng (bao gồm cả đã xóa) với filter và phân trang.
    /// Admin có thể xem tất cả bài kể cả OnlyMe và đã xóa.
    /// </summary>
    /// <response code="200">PagedResult&lt;AdminPostDto&gt;.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("posts")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminPostDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllPosts([FromQuery] AdminPostQueryDto query)
    {
        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.GetAllPosts called. AdminId={AdminId}, Page={Page}, Size={Size}",
            adminId, query.Page, query.Size);

        var result = await _adminService.GetAllPostsAsync(query);
        return Ok(ApiResponse<PagedResult<AdminPostDto>>.Ok(result, "Danh sách bài đăng."));
    }



    /// <summary>
    /// Admin xóa mềm bài đăng, ghi lý do và audit log.
    /// Media trên cloud được giữ lại cho mục đích audit.
    /// </summary>
    /// <param name="id">Id của bài đăng cần xóa.</param>
    /// <param name="dto">Body chứa lý do xóa.</param>
    /// <response code="204">Xóa thành công.</response>
    /// <response code="400">Id rỗng, body null, hoặc bài đã xóa trước đó.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    /// <response code="422">Validation thất bại (reason quá ngắn/dài).</response>
    [HttpDelete("posts/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DeletePost(
        Guid id,
        [FromBody] AdminDeletePostDto? dto,
        [FromServices] IValidator<AdminDeletePostDto> validator)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("Id bài đăng không hợp lệ."));

        if (dto is null)
            return BadRequest(ApiResponse<object>.BadRequest("Body không được để trống."));

        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse<object>.BadRequest(
                validation.Errors.Select(e => e.ErrorMessage)));

        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.DeletePost called. AdminId={AdminId}, PostId={PostId}",
            adminId, id);

        try
        {
            await _adminService.AdminDeletePostAsync(adminId, id, dto.Reason);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Khôi phục bài đăng đã bị xóa mềm.
    /// </summary>
    /// <param name="id">Id của bài đăng cần khôi phục.</param>
    /// <response code="200">Khôi phục thành công.</response>
    /// <response code="400">Id rỗng hoặc bài chưa bị xóa.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpPut("posts/{id:guid}/restore")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RestorePost(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("Id bài đăng không hợp lệ."));

        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.RestorePost called. AdminId={AdminId}, PostId={PostId}",
            adminId, id);

        try
        {
            await _adminService.AdminRestorePostAsync(adminId, id);
            return Ok(ApiResponse<object>.Ok("Khôi phục bài đăng thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Lấy danh sách user với filter và phân trang.
    /// PasswordHash không bao giờ được trả ra ngoài.
    /// </summary>
    /// <response code="200">PagedResult&lt;AdminUserDto&gt;.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("users")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminUserDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllUsers([FromQuery] AdminUserQueryDto query)
    {
        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.GetAllUsers called. AdminId={AdminId}, Page={Page}, Size={Size}",
            adminId, query.Page, query.Size);

        var result = await _adminService.GetAllUsersAsync(query);
        return Ok(ApiResponse<PagedResult<AdminUserDto>>.Ok(result, "Danh sách người dùng."));
    }



    /// <summary>
    /// Cấm tài khoản user: set IsBanned, revoke toàn bộ refresh token,
    /// xóa cache BannedUser middleware, tạo system notification.
    /// </summary>
    /// <param name="id">Id của user cần cấm.</param>
    /// <param name="dto">Body chứa lý do cấm.</param>
    /// <response code="204">Cấm thành công.</response>
    /// <response code="400">Id rỗng, tự cấm mình, hoặc user đã bị cấm.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin hoặc target là Admin khác.</response>
    /// <response code="404">User không tồn tại.</response>
    /// <response code="422">Validation thất bại.</response>
    [HttpPut("users/{id:guid}/ban")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> BanUser(
        Guid id,
        [FromBody] BanUserDto? dto,
        [FromServices] IValidator<BanUserDto> validator)
    {
        var adminId = User.GetUserIdOrThrow();

        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("Id người dùng không hợp lệ."));

        if (id == adminId)
            return BadRequest(ApiResponse<object>.BadRequest("Không thể tự cấm tài khoản của mình."));

        if (dto is null)
            return BadRequest(ApiResponse<object>.BadRequest("Body không được để trống."));

        var validation = await validator.ValidateAsync(dto);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse<object>.BadRequest(
                validation.Errors.Select(e => e.ErrorMessage)));

        _logger.LogInformation(
            "AdminController.BanUser called. AdminId={AdminId}, TargetId={TargetId}",
            adminId, id);

        try
        {
            await _adminService.BanUserAsync(adminId, id, dto.Reason);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Gỡ lệnh cấm tài khoản user, tạo system notification thông báo cho user.
    /// </summary>
    /// <param name="id">Id của user cần gỡ cấm.</param>
    /// <response code="204">Gỡ cấm thành công.</response>
    /// <response code="400">Id rỗng hoặc user không bị cấm.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    /// <response code="404">User không tồn tại.</response>
    [HttpPut("users/{id:guid}/unban")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnbanUser(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("Id người dùng không hợp lệ."));

        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.UnbanUser called. AdminId={AdminId}, TargetId={TargetId}",
            adminId, id);

        try
        {
            await _adminService.UnbanUserAsync(adminId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Lấy thống kê cloud storage (Cloudinary + R2).
    /// Kết quả được cache 10 phút.
    /// </summary>
    /// <response code="200">AdminCloudStatsDto.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpGet("cloud/stats")]
    [ProducesResponseType(typeof(ApiResponse<AdminCloudStatsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCloudStats()
    {
        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.GetCloudStats called. AdminId={AdminId}", adminId);

        try
        {
            var result = await _adminService.GetCloudStatsAsync();
            return Ok(ApiResponse<AdminCloudStatsDto>.Ok(result, "Thống kê cloud storage."));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Xóa file trực tiếp trên cloud storage.
    /// Nếu PostMediaFileId có giá trị → xóa luôn record PostMediaFile trong DB.
    /// </summary>
    /// <param name="dto">Body chứa thông tin file cần xóa.</param>
    /// <response code="204">Xóa thành công.</response>
    /// <response code="400">PublicIdOrKey rỗng hoặc body null.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không phải Admin.</response>
    [HttpDelete("cloud/file")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteCloudFile([FromBody] AdminDeleteCloudFileDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<object>.BadRequest("Body không được để trống."));

        if (string.IsNullOrWhiteSpace(dto.PublicIdOrKey))
            return BadRequest(ApiResponse<object>.BadRequest("PublicIdOrKey không được để trống."));

        var adminId = User.GetUserIdOrThrow();
        _logger.LogInformation(
            "AdminController.DeleteCloudFile called. AdminId={AdminId}, Key={Key}, Provider={Provider}",
            adminId, dto.PublicIdOrKey, dto.Provider);

        try
        {
            await _adminService.AdminDeleteCloudFileAsync(adminId, dto);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.BadRequest(ex.Message));
        }
    }
}