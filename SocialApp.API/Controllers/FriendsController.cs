using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Friends;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;
using SocialApp.Domain.Enums;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý kết bạn: gửi/chấp nhận/từ chối lời mời, hủy kết bạn,
/// block/unblock, danh sách bạn bè, gợi ý kết bạn, trạng thái quan hệ.
/// Toàn bộ endpoint yêu cầu đã đăng nhập — [Authorize] đặt ở cấp controller.
/// </summary>
[ApiController]
[Route("api/friends")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("default")]
public sealed class FriendsController : ControllerBase
{
    private readonly IFriendService _friendService;
    private readonly IValidator<FriendRequestDto> _friendRequestValidator;
    private readonly ILogger<FriendsController> _logger;

    public FriendsController(
        IFriendService friendService,
        IValidator<FriendRequestDto> friendRequestValidator,
        ILogger<FriendsController> logger)
    {
        _friendService = friendService;
        _friendRequestValidator = friendRequestValidator;
        _logger = logger;
    }



    /// <summary>Gửi lời mời kết bạn.</summary>
    /// <response code="201">Gửi thành công hoặc cross-request auto-accept.</response>
    /// <response code="400">Input không hợp lệ, đã là bạn, đã gửi rồi, đang block...</response>
    /// <response code="404">Receiver không tồn tại hoặc bị receiver block.</response>
    /// <response code="422">Validation errors.</response>
    [HttpPost("request")]
    [ProducesResponseType(typeof(ApiResponse<FriendResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendRequest([FromBody] FriendRequestDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest("Body không được để trống."));

        var validation = await _friendRequestValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage);
            return UnprocessableEntity(ApiResponse<FriendResponseDto>.UnprocessableEntity(errors));
        }

        var senderId = User.GetUserIdOrThrow();

        try
        {
            var result = await _friendService.SendRequestAsync(senderId, dto.ReceiverId);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<FriendResponseDto>.Created(result, "Đã gửi lời mời kết bạn."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<FriendResponseDto>.NotFound(ex.Message));
        }
    }



    /// <summary>Chấp nhận lời mời kết bạn.</summary>
    /// <response code="200">Chấp nhận thành công.</response>
    /// <response code="400">Lời mời không còn hiệu lực.</response>
    /// <response code="403">Không phải receiver của lời mời.</response>
    /// <response code="404">Lời mời không tồn tại.</response>
    [HttpPut("request/{id:guid}/accept")]
    [ProducesResponseType(typeof(ApiResponse<FriendResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AcceptRequest(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest("RequestId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var result = await _friendService.AcceptRequestAsync(userId, id);
            return Ok(ApiResponse<FriendResponseDto>.Ok(result, "Đã chấp nhận lời mời kết bạn."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<FriendResponseDto>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<FriendResponseDto>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest(ex.Message));
        }
    }



    /// <summary>Từ chối lời mời kết bạn.</summary>
    /// <response code="200">Từ chối thành công.</response>
    /// <response code="400">Lời mời không còn hiệu lực.</response>
    /// <response code="403">Không phải receiver của lời mời.</response>
    /// <response code="404">Lời mời không tồn tại.</response>
    [HttpPut("request/{id:guid}/reject")]
    [ProducesResponseType(typeof(ApiResponse<FriendResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RejectRequest(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest("RequestId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var result = await _friendService.RejectRequestAsync(userId, id);
            return Ok(ApiResponse<FriendResponseDto>.Ok(result, "Đã từ chối lời mời kết bạn."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<FriendResponseDto>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<FriendResponseDto>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<FriendResponseDto>.BadRequest(ex.Message));
        }
    }



    /// <summary>Hủy kết bạn với một user.</summary>
    /// <response code="204">Hủy kết bạn thành công.</response>
    /// <response code="400">TargetId không hợp lệ hoặc không phải bạn bè.</response>
    /// <response code="404">Quan hệ bạn bè không tồn tại.</response>
    [HttpDelete("{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Unfriend(Guid targetId)
    {
        if (targetId == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("TargetId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _friendService.UnfriendAsync(userId, targetId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }



    /// <summary>Chặn một user.</summary>
    /// <response code="204">Chặn thành công.</response>
    /// <response code="400">TargetId không hợp lệ, tự chặn mình, hoặc đã chặn rồi.</response>
    /// <response code="404">User không tồn tại.</response>
    [HttpPost("block/{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> BlockUser(Guid targetId)
    {
        if (targetId == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("TargetId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _friendService.BlockUserAsync(userId, targetId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }



    /// <summary>Bỏ chặn một user.</summary>
    /// <response code="204">Bỏ chặn thành công.</response>
    /// <response code="404">Không tìm thấy lệnh chặn.</response>
    [HttpDelete("block/{targetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnblockUser(Guid targetId)
    {
        if (targetId == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("TargetId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _friendService.UnblockUserAsync(userId, targetId);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
    }



    /// <summary>Lấy danh sách bạn bè của user hiện tại.</summary>
    /// <response code="200">Danh sách bạn bè phân trang, OrderBy FullName ASC.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FriendListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFriends(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _friendService.GetFriendsAsync(userId, page, size);
        return Ok(ApiResponse<PagedResult<FriendListItemDto>>.Ok(result, "Danh sách bạn bè."));
    }



    /// <summary>Lấy danh sách lời mời kết bạn đang chờ xác nhận (user là receiver).</summary>
    /// <response code="200">Danh sách pending requests phân trang, OrderBy CreatedAt DESC.</response>
    [HttpGet("requests/pending")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FriendResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetPendingRequests(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _friendService.GetPendingRequestsAsync(userId, page, size);
        return Ok(ApiResponse<PagedResult<FriendResponseDto>>.Ok(result, "Danh sách lời mời chờ xác nhận."));
    }



    /// <summary>Lấy danh sách lời mời kết bạn đã gửi đi (user là sender, status = Pending).</summary>
    /// <response code="200">Danh sách sent requests phân trang, OrderBy CreatedAt DESC.</response>
    [HttpGet("requests/sent")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FriendResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSentRequests(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _friendService.GetSentRequestsAsync(userId, page, size);
        return Ok(ApiResponse<PagedResult<FriendResponseDto>>.Ok(result, "Danh sách lời mời đã gửi."));
    }



    /// <summary>Gợi ý kết bạn dựa trên friends-of-friends.</summary>
    /// <response code="200">Danh sách gợi ý phân trang, OrderBy MutualFriendsCount DESC.</response>
    [HttpGet("suggestions")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<FriendSuggestionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSuggestions(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _friendService.GetSuggestionsAsync(userId, page, size);
        return Ok(ApiResponse<PagedResult<FriendSuggestionDto>>.Ok(result, "Gợi ý kết bạn."));
    }



    /// <summary>
    /// Lấy trạng thái quan hệ giữa user hiện tại và targetId.
    /// Trả { status: "Self" } nếu targetId == userId mà không gọi service.
    /// </summary>
    /// <response code="200">Trạng thái quan hệ.</response>
    /// <response code="400">TargetId không hợp lệ.</response>
    [HttpGet("status/{targetId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFriendshipStatus(Guid targetId)
    {
        if (targetId == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("TargetId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        if (targetId == userId)
        {
            return Ok(ApiResponse<object>.Ok(
                new { status = "Self" },
                "Trạng thái quan hệ."));
        }

        var status = await _friendService.GetFriendshipStatusAsync(userId, targetId);

        var statusString = (int)status == 99 ? "None" : status.ToString();

        return Ok(ApiResponse<object>.Ok(
            new { status = statusString },
            "Trạng thái quan hệ."));
    }
}