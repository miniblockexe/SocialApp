using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý bình luận độc lập với route /api/posts — hiện chỉ có xóa.
/// Tạo/list comment nằm ở PostsController vì luôn gắn với 1 bài đăng cụ thể.
/// </summary>
[ApiController]
[Route("api/comments")]
[Produces("application/json")]
[Authorize]
public sealed class CommentsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly ILogger<CommentsController> _logger;

    public CommentsController(IPostService postService, ILogger<CommentsController> logger)
    {
        _postService = postService;
        _logger = logger;
    }



    /// <summary>Soft-delete bình luận.</summary>
    /// <response code="204">Xóa thành công — không trả body.</response>
    /// <response code="400">Bình luận đã được xóa trước đó.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Không phải chủ bình luận và không phải admin.</response>
    /// <response code="404">Bình luận không tồn tại.</response>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        var userId = User.GetUserIdOrThrow();
        var isAdmin = User.IsAdmin();

        try
        {
            await _postService.DeleteCommentAsync(userId, id, isAdmin);

            if (isAdmin)
            {
                _logger.LogWarning(
                    "[DELETE /api/comments/{CommentId}] Admin xóa bình luận — AdminId: {AdminId}", id, userId);
            }

            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
    }
}