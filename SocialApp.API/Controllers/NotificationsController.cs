using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Notifications;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý thông báo: lấy danh sách, đếm chưa đọc,
/// đánh dấu đã đọc (1 hoặc tất cả), xóa.
/// Toàn bộ endpoint yêu cầu đã đăng nhập.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("default")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        INotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }



    /// <summary>Lấy danh sách thông báo của user hiện tại (OrderBy CreatedAt DESC).</summary>
    /// <response code="200">Danh sách notification phân trang.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _notificationService.GetNotificationsAsync(userId, page, size);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(result, "Danh sách thông báo."));
    }



    /// <summary>Lấy số lượng thông báo chưa đọc và tổng số thông báo.</summary>
    /// <response code="200">NotificationCountDto.</response>
    [HttpGet("count")]
    [ProducesResponseType(typeof(ApiResponse<NotificationCountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _notificationService.GetUnreadCountAsync(userId);
        return Ok(ApiResponse<NotificationCountDto>.Ok(result, "Số lượng thông báo."));
    }



    /// <summary>
    /// Đánh dấu một hoặc nhiều notification là đã đọc.
    /// Notification không thuộc về user → silent ignore.
    /// </summary>
    /// <response code="204">Đánh dấu thành công (hoặc no-op nếu danh sách rỗng).</response>
    /// <response code="400">Body null hoặc không hợp lệ.</response>
    [HttpPut("read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAsRead([FromBody] MarkReadDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<object>.BadRequest("Body không được để trống."));

        // NotificationIds null → xử lý như list rỗng (no-op)
        var ids = dto.NotificationIds ?? [];

        var validIds = ids.Where(id => id != Guid.Empty).ToList();

        var userId = User.GetUserIdOrThrow();
        await _notificationService.MarkAsReadAsync(userId, validIds);
        return NoContent();
    }



    /// <summary>Đánh dấu toàn bộ notification chưa đọc của user là đã đọc.</summary>
    /// <response code="204">Thành công.</response>
    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.GetUserIdOrThrow();
        await _notificationService.MarkAllAsReadAsync(userId);
        return NoContent();
    }



    /// <summary>Xóa một notification. Chỉ owner mới được xóa.</summary>
    /// <response code="204">Xóa thành công.</response>
    /// <response code="400">Id không hợp lệ.</response>
    /// <response code="403">Không phải owner của notification.</response>
    /// <response code="404">Notification không tồn tại.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("NotificationId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _notificationService.DeleteNotificationAsync(userId, id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Forbidden(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }
}