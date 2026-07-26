using Microsoft.Extensions.Logging;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Lọc từ ngữ tục tĩu qua PurgoMalum API.
/// Hoàn toàn miễn phí, không cần API key.
/// Tài liệu: http://www.purgomalum.com
/// </summary>
public sealed class PurgoMalumService : IProfanityFilterService
{
    private readonly HttpClient _http;
    private readonly ILogger<PurgoMalumService> _logger;

    // PurgoMalum xử lý tối đa ~2000 ký tự một lần
    private const int MaxTextLength = 1900;

    public PurgoMalumService(HttpClient http, ILogger<PurgoMalumService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<bool> ContainsProfanityAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        // Truncate — nếu phần đầu 1900 ký tự sạch, chấp nhận rủi ro còn lại
        var chunk = text.Length > MaxTextLength ? text[..MaxTextLength] : text;

        try
        {
            var url = $"https://www.purgomalum.com/service/containsprofanity?text={Uri.EscapeDataString(chunk)}";
            var result = await _http.GetStringAsync(url, ct);

            return result.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            // Fail-open: API lỗi không chặn user
            _logger.LogWarning(ex, "[PurgoMalum] Check thất bại — skip filter");
            return false;
        }
    }
}
