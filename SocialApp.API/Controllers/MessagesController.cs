using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Application.DTOs.Emoji;
using SocialApp.Application.DTOs.Tenor;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Quản lý conversation và tin nhắn.
/// Toàn bộ endpoint yêu cầu đã đăng nhập — [Authorize] đặt ở cấp controller.
///
/// Route map:
///   POST   /api/conversations                     tạo hoặc lấy conversation
///   GET    /api/conversations                     danh sách conversation của user
///   GET    /api/conversations/{id}/messages       danh sách tin nhắn (kèm auto mark seen)
///   POST   /api/conversations/{id}/messages       gửi tin nhắn (HTTP, hỗ trợ file)
///   PUT    /api/conversations/{id}/seen           đánh dấu đã đọc
///   DELETE /api/messages/{id}                     xóa tin nhắn (soft delete)
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting("default")]
public sealed class MessagesController : ControllerBase
{
    private readonly IMessageService _messageService;
    private readonly IValidator<CreateConversationDto> _createConversationValidator;
    private readonly IValidator<SendMessageDto> _sendMessageValidator;
    private readonly ITenorService _tenorService;
    private readonly IEmojiService _emojiService;
    private readonly ILogger<MessagesController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    // Upload file tối đa 50MB — kiểm tra nhanh ở controller trước khi vào service
    private const long MaxAttachmentBytes = 50 * 1024 * 1024;

    public MessagesController(
        IMessageService messageService,
        IValidator<CreateConversationDto> createConversationValidator,
        IValidator<SendMessageDto> sendMessageValidator,
        ITenorService tenorService,
        IEmojiService emojiService,
        ILogger<MessagesController> logger,
        IServiceScopeFactory scopeFactory)
    {
        _messageService = messageService;
        _createConversationValidator = createConversationValidator;
        _sendMessageValidator = sendMessageValidator;
        _tenorService = tenorService;
        _emojiService = emojiService;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }



    /// <summary>
    /// Tạo mới hoặc trả về conversation đã tồn tại (idempotent).
    /// Với conversation 1-1: nếu đã có  trả conversation cũ, không tạo mới.
    /// Với group: luôn tạo mới.
    /// </summary>
    /// <response code="200">Thành công — trả về ConversationDto.</response>
    /// <response code="400">Dữ liệu đầu vào không hợp lệ.</response>
    /// <response code="404">Người dùng trong danh sách không tồn tại.</response>
    /// <response code="422">Validation errors.</response>
    [HttpPost("conversations")]
    [ProducesResponseType(typeof(ApiResponse<ConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateOrGetConversation([FromBody] CreateConversationDto? dto)
    {
        if (dto is null)
            return BadRequest(ApiResponse<ConversationDto>.BadRequest("Body không được để trống."));

        var validation = await _createConversationValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<ConversationDto>.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            var conversation = await _messageService.CreateOrGetConversationAsync(userId, dto);
            return Ok(ApiResponse<ConversationDto>.Ok(conversation));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<ConversationDto>.BadRequest(ex.Message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ConversationDto>.NotFound(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CreateOrGetConversation thất bại. UserId={UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<ConversationDto>.InternalServerError());
        }
    }



    /// <summary>
    /// Lấy danh sách conversation của user hiện tại, sắp xếp theo tin nhắn mới nhất.
    /// </summary>
    /// <response code="200">Danh sách conversation (có phân trang).</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ConversationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        Response.Headers.CacheControl = "no-store";

        var userId = User.GetUserIdOrThrow();

        try
        {
            var result = await _messageService.GetConversationsAsync(userId, page, size);
            return Ok(ApiResponse<PagedResult<ConversationDto>>.Ok(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetConversations thất bại. UserId={UserId}", userId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PagedResult<ConversationDto>>.InternalServerError());
        }
    }



    /// <summary>
    /// Lấy danh sách tin nhắn trong conversation — tin mới nhất đầu tiên.
    /// Tự động đánh dấu đã đọc trong background sau khi trả response.
    /// </summary>
    /// <response code="200">Danh sách MessageDto (có phân trang).</response>
    /// <response code="400">ConversationId không hợp lệ.</response>
    /// <response code="403">Không có quyền xem conversation này.</response>
    [HttpGet("conversations/{id:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMessages(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        Response.Headers.CacheControl = "no-store";

        if (id == Guid.Empty)
            return BadRequest(ApiResponse<PagedResult<MessageDto>>.BadRequest("ConversationId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var result = await _messageService.GetMessagesAsync(userId, id, page, size);
            _ = Task.Run(async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var svc = scope.ServiceProvider.GetRequiredService<IMessageService>();
                    await svc.MarkAsSeenAsync(userId, id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Background MarkAsSeen thất bại. UserId={UserId}, ConvId={ConvId}",
                        userId, id);
                }
            });

            return Ok(ApiResponse<PagedResult<MessageDto>>.Ok(result));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<PagedResult<MessageDto>>.Forbidden(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetMessages thất bại. UserId={UserId}, ConvId={ConvId}", userId, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<PagedResult<MessageDto>>.InternalServerError());
        }
    }



    /// <summary>
    /// Gửi tin nhắn qua HTTP — hỗ trợ text và file đính kèm (ảnh / video / audio).
    /// Tin nhắn text-only nên dùng SignalR SendMessage để giảm latency.
    /// </summary>
    /// <response code="201">Gửi thành công — trả về MessageDto.</response>
    /// <response code="400">Nội dung và file đính kèm đều rỗng, hoặc file không hợp lệ.</response>
    /// <response code="403">Không có quyền gửi tin nhắn trong conversation này.</response>
    /// <response code="413">File vượt quá 50MB.</response>
    /// <response code="422">Validation errors.</response>
    [HttpPost("conversations/{id:guid}/messages")]
    [RequestSizeLimit(MaxAttachmentBytes)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<MessageDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> SendMessage(
        Guid id,
        [FromForm] SendMessageDto? dto)
    {
        // Chặn nhanh trước khi đọc body
        if (Request.ContentLength.HasValue && Request.ContentLength > MaxAttachmentBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge,
                ApiResponse<MessageDto>.PayloadTooLarge("File đính kèm không được vượt quá 50MB."));

        if (id == Guid.Empty)
            return BadRequest(ApiResponse<MessageDto>.BadRequest("ConversationId không hợp lệ."));

        if (dto is null)
            return BadRequest(ApiResponse<MessageDto>.BadRequest("Body không được để trống."));

        // Gắn ConversationId từ route vào dto (sealed class — không dùng được 'with')
        dto = new SendMessageDto
        {
            ConversationId = id,
            Content = dto.Content,
            Attachment = dto.Attachment,
            GifUrl = dto.GifUrl,
            SharedPostId = dto.SharedPostId
        };

        var validation = await _sendMessageValidator.ValidateAsync(dto);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return UnprocessableEntity(ApiResponse<MessageDto>.UnprocessableEntity(errors));
        }

        var userId = User.GetUserIdOrThrow();

        try
        {
            var message = await _messageService.SendMessageAsync(userId, dto);
            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<MessageDto>.Created(message, "Gửi tin nhắn thành công."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<MessageDto>.BadRequest(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<MessageDto>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MessageDto>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SendMessage thất bại. UserId={UserId}, ConvId={ConvId}", userId, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<MessageDto>.InternalServerError());
        }
    }



    /// <summary>
    /// Đánh dấu đã đọc toàn bộ tin nhắn trong conversation.
    /// Idempotent — gọi nhiều lần không gây lỗi.
    /// </summary>
    /// <response code="204">Đã cập nhật thành công.</response>
    /// <response code="403">Không có quyền trong conversation này.</response>
    [HttpPut("conversations/{id:guid}/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAsSeen(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("ConversationId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            await _messageService.MarkAsSeenAsync(userId, id);
            return NoContent();
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Forbidden(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "MarkAsSeen thất bại. UserId={UserId}, ConvId={ConvId}", userId, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.InternalServerError());
        }
    }



    /// <summary>
    /// Soft delete tin nhắn — chỉ được xóa tin của chính mình trong vòng 24 giờ.
    /// Trả về MessageDto với IsDeleted = true.
    /// </summary>
    /// <response code="200">Xóa thành công — trả về MessageDto với IsDeleted=true.</response>
    /// <response code="400">Tin nhắn đã xóa, hoặc quá 24 giờ.</response>
    /// <response code="403">Không phải tin nhắn của bạn.</response>
    /// <response code="404">Tin nhắn không tồn tại.</response>
    [HttpDelete("messages/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<MessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMessage(Guid id)
    {
        if (id == Guid.Empty)
            return BadRequest(ApiResponse<MessageDto>.BadRequest("MessageId không hợp lệ."));

        var userId = User.GetUserIdOrThrow();

        try
        {
            var message = await _messageService.DeleteMessageAsync(userId, id);
            return Ok(ApiResponse<MessageDto>.Ok(message, "Tin nhắn đã được xóa."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MessageDto>.NotFound(ex.Message));
        }
        catch (ForbiddenException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<MessageDto>.Forbidden(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MessageDto>.BadRequest(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "DeleteMessage thất bại. UserId={UserId}, MessageId={MessageId}", userId, id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<MessageDto>.InternalServerError());
        }
    }



    /// <summary>
    /// Lấy toàn bộ danh sách emoji (EmojiHub). Kết quả được cache 24h.
    /// Dùng để hiển thị emoji picker trong chat.
    /// </summary>
    [HttpGet("emojis")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmojiDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmojis(CancellationToken ct)
    {
        var emojis = await _emojiService.GetAllAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<EmojiDto>>.Ok(emojis));
    }



    /// <summary>
    /// Lấy emoji theo category.
    /// Category hợp lệ: smileys-and-people, animals-and-nature, food-and-drink,
    /// travel-and-places, activities, objects, symbols, flags.
    /// </summary>
    [HttpGet("emojis/{category}")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<EmojiDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEmojisByCategory(string category, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(category))
            return BadRequest(ApiResponse<object>.BadRequest("Category không được để trống."));

        var emojis = await _emojiService.GetByCategoryAsync(category.ToLowerInvariant(), ct);
        return Ok(ApiResponse<IReadOnlyList<EmojiDto>>.Ok(emojis));
    }



    /// <summary>
    /// Tìm kiếm GIF từ Tenor theo từ khóa.
    /// Trả về danh sách GIF kèm preview URL và full GIF URL.
    /// </summary>
    /// <param name="q">Từ khóa tìm kiếm.</param>
    /// <param name="limit">Số kết quả trả về (mặc định 20, tối đa 50).</param>
    /// <param name="pos">Cursor từ response trước để load thêm (pagination).</param>
    [HttpGet("gifs/search")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<TenorSearchResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchGifs(
        [FromQuery] string? q,
        [FromQuery] int limit = 20,
        [FromQuery] string? pos = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(ApiResponse<object>.BadRequest("Query 'q' không được để trống."));

        var result = await _tenorService.SearchAsync(q, Math.Clamp(limit, 1, 50), pos, ct);
        return Ok(ApiResponse<TenorSearchResult>.Ok(result));
    }



    /// <summary>
    /// Lấy GIF đang trending từ Tenor.
    /// </summary>
    /// <param name="limit">Số kết quả (mặc định 20, tối đa 50).</param>
    [HttpGet("gifs/trending")]
    [EnableRateLimiting("default")]
    [ProducesResponseType(typeof(ApiResponse<TenorSearchResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrendingGifs([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var result = await _tenorService.TrendingAsync(Math.Clamp(limit, 1, 50), ct);
        return Ok(ApiResponse<TenorSearchResult>.Ok(result));
    }
}