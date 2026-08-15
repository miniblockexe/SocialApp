using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý bài đăng: CRUD, feed, toggle like, comment (kèm reply 1 cấp).
/// Toàn bộ endpoint yêu cầu đã đăng nhập — [Authorize] đặt ở cấp controller.
/// </summary>
[ApiController]
[Route("api/posts")]
[Produces("application/json")]
[Authorize]
public sealed class PostsController : ControllerBase
{
    private readonly IPostService _postService;
    private readonly IValidator<CreatePostDto> _createPostValidator;
    private readonly IValidator<CreateCommentDto> _createCommentValidator;
    private readonly ILogger<PostsController> _logger;

    // Trần chặn nhanh cho tổng request multipart (10 file media) — heuristic, không phải
    // tổng chính xác theo từng loại. Giới hạn CHÍNH XÁC theo từng file/loại nằm ở PostService
    // (FileValidationSettings cho ảnh, CloudflareR2Settings cho video/audio).
    private const long MaxCreatePostRequestBytes = 600 * 1024 * 1024; // 600MB

    public PostsController(
        IPostService postService,
        IValidator<CreatePostDto> createPostValidator,
        IValidator<CreateCommentDto> createCommentValidator,
        ILogger<PostsController> logger)
    {
        _postService = postService;
        _createPostValidator = createPostValidator;
        _createCommentValidator = createCommentValidator;
        _logger = logger;
    }



    /// <summary>Tạo bài đăng mới — phải có Content hoặc ít nhất 1 file media.</summary>
    /// <response code="201">Tạo thành công — trả về bài đăng vừa tạo.</response>
    /// <response code="400">Content và MediaFiles đều rỗng, hoặc file sai định dạng.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="413">File media vượt quá giới hạn.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ (validation errors).</response>
    [HttpPost]
    [EnableRateLimiting("default")]
    [RequestSizeLimit(MaxCreatePostRequestBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<PostResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreatePost([FromForm] CreatePostDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<PostResponseDto>.BadRequest("Body không được để trống."));

        var validation = await _createPostValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<PostResponseDto>.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            var post = await _postService.CreatePostAsync(userId, dto);

            _logger.LogInformation(
                "[POST /api/posts] Tạo bài đăng thành công — PostId: {PostId}, UserId: {UserId}",
                post.Id, userId);

            return CreatedAtAction(nameof(GetPost), new { id = post.Id },
                ApiResponse<PostResponseDto>.Created(post, "Đăng bài thành công."));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<PostResponseDto>.PayloadTooLarge(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<PostResponseDto>.BadRequest(ex.Message));
        }
        // InvalidOperationException (upload cloud thất bại giữa chừng, đã rollback) → để
        // GlobalExceptionMiddleware map 500 mặc định, không bắt riêng ở đây.
    }



    /// <summary>Cập nhật Content/Privacy bài đăng. Không thêm/xóa media qua endpoint này.</summary>
    /// <response code="200">Cập nhật thành công.</response>
    /// <response code="400">Body rỗng.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Không phải chủ bài viết.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpPut("{id:guid}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PostResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePost(Guid id, [FromBody] UpdatePostDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<PostResponseDto>.BadRequest("Body không được để trống."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var post = await _postService.UpdatePostAsync(userId, id, dto);
            return Ok(ApiResponse<PostResponseDto>.Ok(post, "Cập nhật bài đăng thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PostResponseDto>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PostResponseDto>.Forbidden(ex.Message));
        }
    }



    /// <summary>Soft-delete bài đăng. Media trên cloud được giữ lại (audit).</summary>
    /// <response code="204">Xóa thành công — không trả body.</response>
    /// <response code="400">Bài viết đã được xóa trước đó.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Không phải chủ bài viết và không phải admin.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpDelete("{id:guid}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePost(Guid id)
    {
        var userId = User.GetUserIdOrThrow();
        var isAdmin = User.IsAdmin();

        try
        {
            await _postService.DeletePostAsync(userId, id, isAdmin);

            if (isAdmin)
            {
                _logger.LogWarning(
                    "[DELETE /api/posts/{PostId}] Admin xóa bài viết — AdminId: {AdminId}", id, userId);
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



    /// <summary>Lấy chi tiết 1 bài đăng theo góc nhìn viewer (áp dụng privacy check).</summary>
    /// <response code="200">Trả về bài đăng.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Friends-only và viewer không đủ quyền xem.</response>
    /// <response code="404">Không tồn tại (bao gồm OnlyMe không đủ quyền — cố tình trả 404).</response>
    [HttpGet("{id:guid}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PostResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPost(Guid id)
    {
        var viewerId = User.GetUserIdOrThrow();

        try
        {
            var post = await _postService.GetPostByIdAsync(id, viewerId);
            return Ok(ApiResponse<PostResponseDto>.Ok(post));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PostResponseDto>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<PostResponseDto>.Forbidden(ex.Message));
        }
    }



    /// <summary>
    /// Feed: bài của bạn bè (trừ OnlyMe) + bài của chính mình (mọi privacy)
    /// + bài Public của người lạ. Hỗ trợ cursor-based pagination qua CursorId.
    /// </summary>
    /// <response code="200">Danh sách bài đăng phân trang.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    [HttpGet("feed")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetFeed([FromQuery] FeedQueryDto query)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _postService.GetFeedAsync(userId, query);
        return Ok(ApiResponse<PagedResult<PostResponseDto>>.Ok(result));
    }



    /// <summary>Lấy danh sách bài đăng của 1 user cụ thể (áp dụng privacy check theo viewer).</summary>
    /// <param name="id">Id chủ trang cần xem bài.</param>
    /// <param name="page">Trang hiện tại (mặc định 1).</param>
    /// <param name="size">Số kết quả mỗi trang (mặc định 10, tối đa 100).</param>
    /// <response code="200">Danh sách bài đăng phân trang.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    [HttpGet("/api/users/{id:guid}/posts")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PostResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUserPosts(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var viewerId = User.GetUserIdOrThrow();
        var result = await _postService.GetUserPostsAsync(id, viewerId, page, size);
        return Ok(ApiResponse<PagedResult<PostResponseDto>>.Ok(result));
    }



    /// <summary>Toggle like — đã like thì unlike, chưa like thì like.</summary>
    /// <response code="200">Trả về trạng thái like mới: { isLiked: bool }.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="403">Friends-only và không đủ quyền xem bài.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpPost("{id:guid}/like")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleLike(Guid id)
    {
        var userId = User.GetUserIdOrThrow();

        try
        {
            var isLiked = await _postService.ToggleLikeAsync(userId, id);
            var message = isLiked ? "Đã thích bài viết." : "Đã bỏ thích bài viết.";
            return Ok(ApiResponse<object>.Ok(new { isLiked }, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Forbidden(ex.Message));
        }
    }



    /// <summary>Lấy danh sách bình luận gốc của 1 bài đăng (kèm RepliesCount mỗi comment).</summary>
    /// <param name="id">Id bài đăng.</param>
    /// <param name="page">Trang hiện tại (mặc định 1).</param>
    /// <param name="size">Số kết quả mỗi trang (mặc định 10, tối đa 100).</param>
    /// <response code="200">Danh sách bình luận phân trang.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpGet("{id:guid}/comments")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CommentResponseDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetComments(Guid id, [FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var viewerId = User.GetUserIdOrThrow();

        try
        {
            var result = await _postService.GetCommentsAsync(id, viewerId, page, size);
            return Ok(ApiResponse<PagedResult<CommentResponseDto>>.Ok(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PagedResult<CommentResponseDto>>.NotFound(ex.Message));
        }
    }



    /// <summary>Thêm bình luận, hoặc reply vào 1 comment gốc (chỉ hỗ trợ 1 cấp).</summary>
    /// <response code="201">Bình luận thành công.</response>
    /// <response code="400">Reply vào reply, hoặc parent comment không thuộc bài này.</response>
    /// <response code="401">Chưa đăng nhập hoặc JWT không hợp lệ.</response>
    /// <response code="404">Bài đăng hoặc parent comment không tồn tại.</response>
    /// <response code="422">Dữ liệu đầu vào không hợp lệ (validation errors).</response>
    [HttpPost("{id:guid}/comments")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<CommentResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddComment(Guid id, [FromBody] CreateCommentDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<CommentResponseDto>.BadRequest("Body không được để trống."));

        var validation = await _createCommentValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<CommentResponseDto>.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            var comment = await _postService.AddCommentAsync(userId, id, dto);

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<CommentResponseDto>.Created(comment, "Bình luận thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<CommentResponseDto>.NotFound(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<CommentResponseDto>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Chia sẻ lại bài viết lên trang cá nhân (repost).
    /// POST /api/posts/{id}/share-to-feed
    /// Body: { content?: string, privacy: 0|1|2 }
    /// </summary>
    [HttpPost("{id:guid}/share-to-feed")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<PostResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ShareToFeed(Guid id, [FromBody] SharePostRequestDto dto)
    {
        var userId = User.GetUserIdOrThrow();
        try
        {
            var result = await _postService.SharePostAsync(userId, id, dto);
            return CreatedAtAction(nameof(GetPost), new { id = result.Id },
                ApiResponse<PostResponseDto>.Ok(result));
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
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ShareToFeed thất bại. PostId={PostId}, UserId={UserId}", id, userId);
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse<object>.InternalServerError());
        }
    }


    /// <summary>Trả về share link của bài viết (backend /share/{id} — tự phục vụ OG preview).</summary>
    /// <response code="200">Trả về share URL.</response>
    /// <response code="404">Bài đăng không tồn tại.</response>
    [HttpGet("{id:guid}/share")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<ShareUrlDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShareUrl(Guid id)
    {
        var userId = User.GetUserIdOrThrow();

        try
        {
            await _postService.GetPostByIdAsync(id, userId);

            var shareUrl = $"{Request.Scheme}://{Request.Host}/share/{id}";

            return Ok(ApiResponse<ShareUrlDto>.Ok(new ShareUrlDto(id, shareUrl, shareUrl)));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.NotFound(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetShareUrl thất bại. PostId={PostId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.InternalServerError());
        }
    }
}