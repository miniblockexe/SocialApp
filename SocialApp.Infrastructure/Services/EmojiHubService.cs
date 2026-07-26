using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SocialApp.Application.DTOs.Emoji;
using SocialApp.Application.Interfaces.Services;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Lấy emoji list từ EmojiHub API — free, không cần key.
/// Kết quả được cache trong memory 24h (emoji thay đổi rất hiếm).
/// Tài liệu: https://github.com/cheatsnake/emojihub
/// </summary>
public sealed class EmojiHubService : IEmojiService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EmojiHubService> _logger;

    private const string BaseUrl = "https://emojihub.yurace.pro/api";
    private const string CacheKeyAll = "emojihub:all";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public EmojiHubService(HttpClient http, IMemoryCache cache, ILogger<EmojiHubService> logger)
    {
        _http  = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmojiDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKeyAll, out IReadOnlyList<EmojiDto>? cached) && cached is not null)
            return cached;

        try
        {
            var json   = await _http.GetStringAsync($"{BaseUrl}/all", ct);
            var result = Parse(json);
            _cache.Set(CacheKeyAll, result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmojiHub] GetAll thất bại");
            return [];
        }
    }

    /// <summary>
    /// Category hợp lệ: smileys-and-people, animals-and-nature, food-and-drink,
    /// travel-and-places, activities, objects, symbols, flags.
    /// </summary>
    public async Task<IReadOnlyList<EmojiDto>> GetByCategoryAsync(string category, CancellationToken ct = default)
    {
        var cacheKey = $"emojihub:cat:{category}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<EmojiDto>? cached) && cached is not null)
            return cached;

        try
        {
            var url    = $"{BaseUrl}/all/category/{Uri.EscapeDataString(category)}";
            var json   = await _http.GetStringAsync(url, ct);
            var result = Parse(json);
            _cache.Set(cacheKey, result, CacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[EmojiHub] GetByCategory thất bại: {Category}", category);
            return [];
        }
    }

    // Private

    private static IReadOnlyList<EmojiDto> Parse(string json)
    {
        using var doc    = JsonDocument.Parse(json);
        var result = new List<EmojiDto>();

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            var name     = item.TryGetProperty("name",     out var n)   ? n.GetString()   ?? "" : "";
            var category = item.TryGetProperty("category", out var cat) ? cat.GetString() ?? "" : "";
            var group    = item.TryGetProperty("group",    out var g)   ? g.GetString()   ?? "" : "";

            var htmlCode = item.TryGetProperty("htmlCode", out var hc)
                ? hc.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : (IReadOnlyList<string>)[];

            var unicode = item.TryGetProperty("unicode", out var uc)
                ? uc.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                : (IReadOnlyList<string>)[];

            result.Add(new EmojiDto(name, category, group, htmlCode, unicode));
        }

        return result;
    }
}
