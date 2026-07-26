using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using SocialApp.API.Extensions;
using SocialApp.Application.Common;
using SocialApp.Application.DTOs.AI;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.API.Controllers;

/// <summary>
/// Endpoint tích hợp Gemini AI: chat với AI assistant và health check.
/// Chat yêu cầu đăng nhập, health check là public.
/// Rate limit AI chat: 10 lần/phút per user (kiểm tra bằng IMemoryCache).
/// </summary>
[ApiController]
[Route("api/ai")]
[Produces("application/json")]
[Authorize]
public sealed class AIController : ControllerBase
{
    private readonly IGeminiService _geminiService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AIController> _logger;

    private const int AiRateLimitPerMinute = 10;

    public AIController(
        IGeminiService geminiService,
        IMemoryCache cache,
        ILogger<AIController> logger)
    {
        _geminiService = geminiService;
        _cache = cache;
        _logger = logger;
    }



    /// <summary>
    /// Gửi tin nhắn tới Gemini AI trong một conversation.
    /// AI response được lưu vào DB và push về client qua SignalR.
    /// Rate limit: 10 lần/phút per user.
    /// </summary>
    /// <param name="dto">Body chứa conversationId, history và newMessage.</param>
    /// <param name="validator">FluentValidation injected.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">GeminiChatResponseDto — content + messageIds + tokensUsed.</response>
    /// <response code="400">ConversationId rỗng hoặc newMessage trống sau trim.</response>
    /// <response code="401">Chưa đăng nhập.</response>
    /// <response code="403">Không có quyền với conversation này.</response>
    /// <response code="422">Validation thất bại.</response>
    /// <response code="429">Vượt quá giới hạn 10 lần/phút.</response>
    [HttpPost("chat")]
    [EnableRateLimiting("gemini")]
    [ProducesResponseType(typeof(ApiResponse<GeminiChatResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Chat(
        [FromBody] GeminiChatRequestDto? dto,
        [FromServices] IValidator<GeminiChatRequestDto> validator,
        CancellationToken cancellationToken)
    {
        if (dto is null)
            return BadRequest(ApiResponse<object>.BadRequest("Body không được để trống."));

        if (dto.ConversationId == Guid.Empty)
            return BadRequest(ApiResponse<object>.BadRequest("ConversationId không hợp lệ."));

        if (string.IsNullOrWhiteSpace(dto.NewMessage))
            return BadRequest(ApiResponse<object>.BadRequest("Tin nhắn không được để trống."));

        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return UnprocessableEntity(ApiResponse<object>.BadRequest(
                validation.Errors.Select(e => e.ErrorMessage)));

        var userId = User.GetUserIdOrThrow();

        // Rate limit per user bằng IMemoryCache
        var cacheKey = $"ai_rate_{userId}";
        if (_cache.TryGetValue(cacheKey, out int requestCount) && requestCount >= AiRateLimitPerMinute)
        {
            _logger.LogWarning(
                "AIController.Chat: Rate limit vượt quá. UserId={UserId}, Count={Count}",
                userId, requestCount);
            return StatusCode(StatusCodes.Status429TooManyRequests,
                ApiResponse<object>.BadRequest("Bạn đã vượt quá giới hạn 10 lần/phút với AI chat."));
        }

        _cache.Set(cacheKey, (requestCount) + 1, TimeSpan.FromMinutes(1));

        _logger.LogInformation(
            "AIController.Chat called. UserId={UserId}, ConvId={ConvId}",
            userId, dto.ConversationId);

        try
        {
            var result = await _geminiService.ChatAsync(userId, dto, cancellationToken);
            return Ok(ApiResponse<GeminiChatResponseDto>.Ok(result, "Phản hồi từ AI."));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.BadRequest(ex.Message));
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                ApiResponse<object>.Forbidden(ex.Message));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest,
                ApiResponse<object>.BadRequest("Yêu cầu bị hủy."));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiResponse<object>.BadRequest(ex.Message));
        }
    }



    /// <summary>
    /// Health check Gemini AI service — không yêu cầu đăng nhập.
    /// Gửi ping tới Gemini API để kiểm tra service còn hoạt động không.
    /// </summary>
    /// <response code="200">{ "available": true/false, "checkedAt": "..." }</response>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> HealthCheck()
    {
        var available = await _geminiService.IsServiceAvailableAsync();
        var checkedAt = DateTime.UtcNow;

        _logger.LogInformation(
            "AIController.HealthCheck: available={Available}, checkedAt={CheckedAt}",
            available, checkedAt);

        return Ok(ApiResponse<object>.Ok(
            new { available, checkedAt = checkedAt.ToString("o") },
            available ? "Dịch vụ AI đang hoạt động." : "Dịch vụ AI hiện không khả dụng."));
    }
}