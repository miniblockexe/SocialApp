using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Groups;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Enums;

namespace SocialApp.API.Controllers;

[ApiController]
[Route("api/groups")]
[Produces("application/json")]
[Authorize]
public sealed class GroupsController : ControllerBase
{
    private readonly IGroupService _groupService;
    private readonly IValidator<CreateGroupDto> _createValidator;
    private readonly IValidator<UpdateGroupDto> _updateValidator;
    private readonly ILogger<GroupsController> _logger;

    public GroupsController(
        IGroupService groupService,
        IValidator<CreateGroupDto> createValidator,
        IValidator<UpdateGroupDto> updateValidator,
        ILogger<GroupsController> logger)
    {
        _groupService = groupService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _logger = logger;
    }

    // ── CRUD ───────────────────────────────────────────────────────────

    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroup([FromForm] CreateGroupDto dto, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse<GroupDetailDto>.BadRequest(
                validation.Errors.Select(e => e.ErrorMessage)));

        var userId = User.GetUserIdOrThrow();
        var group = await _groupService.CreateGroupAsync(userId, dto, ct);
        _logger.LogInformation("[POST /api/groups] GroupId: {GroupId}", group.Id);
        return StatusCode(201, ApiResponse<GroupDetailDto>.Created(group, "Tạo nhóm thành công."));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroup(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<GroupDetailDto>.Ok(await _groupService.GetGroupAsync(id, userId, ct))); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<GroupDetailDto>.NotFound(ex.Message)); }
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GroupSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchGroups([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        return Ok(ApiResponse<PagedResult<GroupSummaryDto>>.Ok(
            await _groupService.SearchGroupsAsync(userId, keyword, page, size, ct)));
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GroupSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyGroups([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        return Ok(ApiResponse<PagedResult<GroupSummaryDto>>.Ok(
            await _groupService.GetMyGroupsAsync(userId, page, size, ct)));
    }

    [HttpPut("{id:guid}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<GroupDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGroup(Guid id, [FromForm] UpdateGroupDto dto, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse<GroupDetailDto>.BadRequest(
                validation.Errors.Select(e => e.ErrorMessage)));

        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<GroupDetailDto>.Ok(await _groupService.UpdateGroupAsync(userId, id, dto, ct), "Cập nhật nhóm thành công.")); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteGroup(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.DeleteGroupAsync(userId, id, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    // ── Member ─────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/join")]
    public async Task<IActionResult> JoinGroup(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<object>.Ok(await _groupService.JoinGroupAsync(userId, id, ct), "Yêu cầu tham gia đã được xử lý.")); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    [HttpPost("{id:guid}/leave")]
    public async Task<IActionResult> LeaveGroup(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.LeaveGroupAsync(userId, id, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    [HttpDelete("{id:guid}/join-request")]
    public async Task<IActionResult> CancelJoinRequest(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.CancelJoinRequestAsync(userId, id, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    [HttpGet("{id:guid}/members")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GroupMemberDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<PagedResult<GroupMemberDto>>.Ok(await _groupService.GetMembersAsync(userId, id, page, size, ct))); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpDelete("{id:guid}/members/{targetUserId:guid}")]
    public async Task<IActionResult> KickMember(Guid id, Guid targetUserId, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.KickMemberAsync(userId, id, targetUserId, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    [HttpPut("{id:guid}/members/{targetUserId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid targetUserId, [FromBody] UpdateMemberRoleDto dto, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.UpdateMemberRoleAsync(userId, id, targetUserId, dto.Role, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    // ── Join Requests ──────────────────────────────────────────────────

    [HttpGet("{id:guid}/join-requests")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GroupJoinRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingJoinRequests(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<PagedResult<GroupJoinRequestDto>>.Ok(await _groupService.GetPendingJoinRequestsAsync(userId, id, page, size, ct))); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpPut("{id:guid}/join-requests/{requestId:guid}")]
    public async Task<IActionResult> ReviewJoinRequest(Guid id, Guid requestId, [FromBody] ApproveJoinRequestDto dto, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.ReviewJoinRequestAsync(userId, id, requestId, dto, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }

    // ── Group Posts ────────────────────────────────────────────────────

    [HttpGet("{id:guid}/posts")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGroupFeed(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 10, [FromQuery] Guid? cursorId = null, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<PagedResult<PostResponseDto>>.Ok(await _groupService.GetGroupFeedAsync(userId, id, page, size, cursorId, ct))); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpPost("{id:guid}/posts")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<PostResponseDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateGroupPost(Guid id, [FromForm] CreateGroupPostDto dto, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try
        {
            var post = await _groupService.CreateGroupPostAsync(userId, id, dto, ct);
            return StatusCode(201, ApiResponse<PostResponseDto>.Created(post, "Đăng bài thành công."));
        }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpGet("{id:guid}/posts/pending")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingPosts(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        try { return Ok(ApiResponse<PagedResult<PostResponseDto>>.Ok(await _groupService.GetPendingPostsAsync(userId, id, page, size, ct))); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
    }

    [HttpPut("{id:guid}/posts/{postId:guid}/review")]
    public async Task<IActionResult> ReviewGroupPost(Guid id, Guid postId, [FromBody] ReviewGroupPostDto dto, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        try { await _groupService.ReviewGroupPostAsync(userId, id, postId, dto, ct); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(ApiResponse<object>.NotFound(ex.Message)); }
        catch (UnauthorizedAccessException ex) { return StatusCode(403, ApiResponse<object>.Forbidden(ex.Message)); }
        catch (InvalidOperationException ex) { return BadRequest(ApiResponse<object>.BadRequest(ex.Message)); }
    }
}
