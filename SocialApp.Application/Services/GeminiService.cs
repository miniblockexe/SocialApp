using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.DTOs.AI;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Services;

/// <summary>
/// Triển khai IGeminiService — gọi Gemini API, lưu message vào DB và push qua SignalR.
/// Tự động fallback sang FallbackModel khi model chính hết quota (429) hoặc không khả dụng (503).
/// KHÔNG bao giờ log ApiKey hoặc expose lỗi nội bộ ra client.
/// </summary>
public sealed class GeminiService : IGeminiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly IMessageDbContext _db;
    private readonly IUserRepository _userRepo;
    private readonly IChatHub _chatHub;
    private readonly ILogger<GeminiService> _logger;

    // System user Id đại diện cho AI bot — phải khớp với seed data hoặc config
    private static readonly Guid AiBotUserId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    // Mã HTTP status cần fallback
    private static readonly HashSet<int> FallbackStatusCodes = [429, 503, 502, 504];

    public GeminiService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiSettings> settings,
        IMessageDbContext db,
        IUserRepository userRepo,
        IChatHub chatHub,
        ILogger<GeminiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _db = db;
        _userRepo = userRepo;
        _chatHub = chatHub;
        _logger = logger;
    }

    // =========================================================================
    // ChatAsync
    // =========================================================================

    public async Task<GeminiChatResponseDto> ChatAsync(
        Guid userId,
        GeminiChatRequestDto request,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId không hợp lệ.");

        if (request.ConversationId == Guid.Empty)
            throw new ArgumentException("ConversationId không hợp lệ.");

        var newMessage = request.NewMessage?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(newMessage))
            throw new ArgumentException("Tin nhắn không được để trống.");

        // Kiểm tra user có trong conversation không
        var isParticipant = await _db.ConversationParticipants
            .AnyAsync(p => p.ConversationId == request.ConversationId
                        && p.UserId == userId, cancellationToken);

        if (!isParticipant)
            throw new UnauthorizedAccessException(
                "Bạn không có quyền gửi tin nhắn trong conversation này.");

        // Sanitize history
        var history = SanitizeHistory(request.History, _settings.MaxHistoryMessages);

        // Build Gemini payload
        var payload = BuildPayload(history, newMessage);

        // Gọi API với auto-fallback
        var (aiContent, tokensUsed, modelUsed) =
            await CallWithFallbackAsync(payload, cancellationToken);

        // Lưu vào DB
        var now = DateTime.UtcNow;

        var userMessage = new Message
        {
            ConversationId = request.ConversationId,
            SenderId = userId,
            Content = newMessage,
            IsAI = false,
            IsDeleted = false,
            CreatedAt = now
        };
        _db.Messages.Add(userMessage);

        var aiMessage = new Message
        {
            ConversationId = request.ConversationId,
            SenderId = AiBotUserId,
            Content = aiContent,
            IsAI = true,
            IsDeleted = false,
            CreatedAt = now.AddMilliseconds(1) // đảm bảo sort sau user message
        };
        _db.Messages.Add(aiMessage);

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(c => c.Id == request.ConversationId, cancellationToken);
        if (conversation is not null)
            conversation.LastMessageAt = now;

        await _db.SaveChangesAsync(cancellationToken);

        // Push qua SignalR
        try
        {
            var aiMessageDto = new MessageDto
            {
                Id = aiMessage.Id,
                ConversationId = aiMessage.ConversationId,
                Content = aiContent,
                IsAI = true,
                AttachmentUrl = null,
                AttachmentType = null,
                CreatedAt = aiMessage.CreatedAt,
                IsDeleted = false,
                Sender = new UserBriefDto
                {
                    Id = AiBotUserId,
                    Username = "AI Assistant",
                    FullName = "SocialApp AI",
                    AvatarUrl = null,
                    Role = Domain.Enums.UserRole.User
                },
                SeenByUserIds = []
            };

            await _chatHub.SendMessageAsync(request.ConversationId, aiMessageDto);
        }
        catch (Exception ex)
        {
            // SignalR fail không ảnh hưởng response — message đã lưu DB
            _logger.LogWarning(ex,
                "GeminiService: Push SignalR thất bại. ConvId={ConvId}",
                request.ConversationId);
        }

        _logger.LogInformation(
            "GeminiService.ChatAsync completed. UserId={UserId}, ConvId={ConvId}, " +
            "Model={Model}, TokensUsed={Tokens}",
            userId, request.ConversationId, modelUsed, tokensUsed);

        return new GeminiChatResponseDto
        {
            Content = aiContent,
            ConversationId = request.ConversationId,
            UserMessageId = userMessage.Id,
            AiMessageId = aiMessage.Id,
            TokensUsed = tokensUsed
        };
    }

    // =========================================================================
    // IsServiceAvailableAsync
    // =========================================================================

    public async Task<bool> IsServiceAvailableAsync()
    {
        // Thử lần lượt từng model trong list
        var modelsToTry = _settings.Models
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct();

        foreach (var model in modelsToTry)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("Gemini");
                var url = $"v1beta/models/{model}:generateContent";

                var payload = new
                {
                    contents = new[]
                    {
                        new { role = "user", parts = new[] { new { text = "ping" } } }
                    },
                    generationConfig = new { maxOutputTokens = 10 }
                };

                var json = JsonSerializer.Serialize(payload);
                var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                requestMsg.Headers.Add("x-goog-api-key", _settings.ApiKey);

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await client.SendAsync(requestMsg, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "GeminiService.IsServiceAvailableAsync: model {Model} khả dụng.", model);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "GeminiService.IsServiceAvailableAsync: model {Model} không phản hồi.", model);
            }
        }

        return false;
    }

    // =========================================================================
    // =========================================================================

    /// <summary>
    /// Gọi Gemini API, thử lần lượt từng model trong Settings.Models.
    /// Khi model trả về 429/503/timeout → tự động thử model tiếp theo trong list.
    /// Trả về (content, tokensUsed, modelUsed).
    /// </summary>
    private async Task<(string Content, int? TokensUsed, string ModelUsed)> CallWithFallbackAsync(
        object payload,
        CancellationToken cancellationToken)
    {
        // Lấy danh sách model từ config, bỏ trùng, bỏ rỗng
        var modelsToTry = _settings.Models
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .ToList();

        if (modelsToTry.Count == 0)
            throw new InvalidOperationException(
                "Chưa cấu hình model Gemini trong appsettings.json.");

        for (int i = 0; i < modelsToTry.Count; i++)
        {
            var model = modelsToTry[i];
            var isLast = i == modelsToTry.Count - 1;

            try
            {
                var (content, tokens) = await CallGeminiAsync(model, payload, cancellationToken);
                return (content, tokens, model);
            }
            catch (GeminiQuotaException ex)
            {
                if (isLast)
                {
                    _logger.LogError(
                        "GeminiService: Tất cả {Count} model đều không khả dụng. " +
                        "Models tried: [{Models}]",
                        modelsToTry.Count,
                        string.Join(", ", modelsToTry));
                    break;
                }

                _logger.LogWarning(
                    "GeminiService: Model [{Model}] không khả dụng (HTTP {StatusCode}) " +
                    "→ thử [{NextModel}]...",
                    model, ex.StatusCode, modelsToTry[i + 1]);
            }
        }

        throw new InvalidOperationException(
            "Dịch vụ AI tạm thời không khả dụng. Vui lòng thử lại sau.");
    }

    /// <summary>
    /// Gọi Gemini API với 1 model cụ thể.
    /// Throw GeminiQuotaException nếu cần fallback (429/503/...).
    /// Throw các exception khác nếu lỗi không thể recover.
    /// </summary>
    private async Task<(string Content, int? TokensUsed)> CallGeminiAsync(
        string model,
        object payload,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Gemini");
        var url = $"v1beta/models/{model}:generateContent";
        var json = JsonSerializer.Serialize(payload);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Add("x-goog-api-key", _settings.ApiKey);

        try
        {
            using var cts = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            var response = await client.SendAsync(httpRequest, cts.Token);
            var statusCode = (int)response.StatusCode;

            if (!response.IsSuccessStatusCode)
            {
                // Các mã cần fallback sang model khác
                if (FallbackStatusCodes.Contains(statusCode))
                    throw new GeminiQuotaException(model, statusCode);

                // Lỗi không thể fallback → throw ngay
                throw statusCode switch
                {
                    400 => new ArgumentException("Yêu cầu không hợp lệ."),
                    401 or 403 => new InvalidOperationException(
                        "API key không hợp lệ hoặc không có quyền truy cập."),
                    _ => new InvalidOperationException(
                        $"Gemini API lỗi HTTP {statusCode}.")
                };
            }

            // Parse response
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream,
                cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var content = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException(
                    "AI không thể tạo phản hồi, vui lòng thử lại.");

            int? tokensUsed = null;
            if (root.TryGetProperty("usageMetadata", out var usage) &&
                usage.TryGetProperty("totalTokenCount", out var tokenProp))
            {
                tokensUsed = tokenProp.GetInt32();
            }

            return (content, tokensUsed);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex,
                "GeminiService: Timeout với model {Model}.", model);
            // Timeout cũng coi như không khả dụng → fallback
            throw new GeminiQuotaException(model, 504);
        }
        catch (TaskCanceledException)
        {
            throw new OperationCanceledException("Yêu cầu bị hủy.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex,
                "GeminiService: Lỗi kết nối với model {Model}.", model);
            // Network error → fallback
            throw new GeminiQuotaException(model, 503);
        }
        catch (GeminiQuotaException) { throw; }
        catch (ArgumentException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GeminiService: Lỗi không xác định với model {Model}.", model);
            throw new InvalidOperationException(
                "Đã xảy ra lỗi khi xử lý yêu cầu AI. Vui lòng thử lại sau.");
        }
    }

    /// <summary>Build Gemini request payload từ history và tin nhắn mới.</summary>
    private object BuildPayload(List<GeminiMessageDto> history, string newMessage)
    {
        var contents = new List<object>();

        foreach (var h in history)
        {
            contents.Add(new
            {
                role = h.Role.ToLower(),
                parts = new[] { new { text = h.Content } }
            });
        }

        contents.Add(new
        {
            role = "user",
            parts = new[] { new { text = newMessage } }
        });

        return new
        {
            system_instruction = new
            {
                parts = new[] { new { text = _settings.SystemPrompt } }
            },
            contents,
            generationConfig = new
            {
                maxOutputTokens = _settings.MaxOutputTokens,
                temperature = _settings.Temperature
            }
        };
    }

    /// <summary>
    /// Giới hạn history và đảm bảo bắt đầu bằng "user" role (yêu cầu của Gemini API).
    /// </summary>
    private static List<GeminiMessageDto> SanitizeHistory(
        List<GeminiMessageDto> history,
        int maxMessages)
    {
        if (history is null || history.Count == 0)
            return [];

        var limited = history.Count > maxMessages
            ? history.Skip(history.Count - maxMessages).ToList()
            : history.ToList();

        // Gemini yêu cầu first message phải là "user"
        while (limited.Count > 0 && limited[0].Role.ToLower() != "user")
            limited.RemoveAt(0);

        return limited;
    }
}

// Internal exception chỉ dùng trong GeminiService

/// <summary>
/// Throw khi model Gemini trả về mã cần fallback (429/503/504/...).
/// Chỉ dùng nội bộ trong GeminiService — không expose ra ngoài.
/// </summary>
file sealed class GeminiQuotaException : Exception
{
    public string Model { get; }
    public int StatusCode { get; }

    public GeminiQuotaException(string model, int statusCode)
        : base($"Model {model} không khả dụng (HTTP {statusCode}).")
    {
        Model = model;
        StatusCode = statusCode;
    }
}