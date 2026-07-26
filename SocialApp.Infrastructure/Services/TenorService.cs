using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialApp.Application.DTOs.Tenor;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Application.Settings;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// GIF search / trending qua Giphy API v1 (thay thế Tenor bị ban).
/// Tài liệu: https://developers.giphy.com/docs/api
/// Free plan: 100 req/giây, không giới hạn tháng.
/// Class vẫn đặt tên TenorService để không phải đổi DI registration.
/// </summary>
public sealed class TenorService : ITenorService
{
    private readonly HttpClient _http;
    private readonly TenorSettings _settings;
    private readonly ILogger<TenorService> _logger;

    // Giphy API v1
    private const string BaseUrl = "https://api.giphy.com/v1/gifs";

    // Rating: g (all ages), pg, pg-13, r
    private const string Rating = "pg-13";

    public TenorService(
        HttpClient http,
        IOptions<TenorSettings> options,
        ILogger<TenorService> logger)
    {
        _http = http;
        _settings = options.Value;
        _logger = logger;
    }

    /// <summary>Tìm GIF theo từ khoá.</summary>
    public async Task<TenorSearchResult> SearchAsync(
        string query, int limit = 20, string? pos = null, CancellationToken ct = default)
    {
        try
        {
            // pos của Giphy là số (offset), không phải cursor string như Tenor
            var offset = int.TryParse(pos, out var o) ? o : 0;

            var url = $"{BaseUrl}/search" +
                      $"?api_key={_settings.ApiKey}" +
                      $"&q={Uri.EscapeDataString(query)}" +
                      $"&limit={Math.Clamp(limit, 1, 50)}" +
                      $"&offset={offset}" +
                      $"&rating={Rating}" +
                      $"&lang=vi";

            return await FetchAsync(url, offset, limit, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Giphy] Search thất bại: {Query}", query);
            return new TenorSearchResult([], null);
        }
    }

    /// <summary>Lấy GIF đang trending.</summary>
    public async Task<TenorSearchResult> TrendingAsync(
        int limit = 20, CancellationToken ct = default)
    {
        try
        {
            var url = $"{BaseUrl}/trending" +
                      $"?api_key={_settings.ApiKey}" +
                      $"&limit={Math.Clamp(limit, 1, 50)}" +
                      $"&rating={Rating}";

            return await FetchAsync(url, 0, limit, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Giphy] Trending thất bại");
            return new TenorSearchResult([], null);
        }
    }

    // Private

    /// <summary>
    /// Parse Giphy API response → TenorSearchResult.
    /// Giphy response format:
    /// {
    ///   "data": [ { "id", "title", "images": {
    ///     "original":            { "url" },
    ///     "fixed_height":        { "url" },   ← medium
    ///     "fixed_height_small":  { "url" },   ← tiny + preview
    ///   } } ],
    ///   "pagination": { "offset", "total_count", "count" }
    /// }
    /// </summary>
    private async Task<TenorSearchResult> FetchAsync(
        string url, int currentOffset, int limit, CancellationToken ct)
    {
        using var response = await _http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var results = new List<TenorGifDto>();

        if (root.TryGetProperty("data", out var arr))
        {
            foreach (var item in arr.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var title = item.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? "" : "";

                string? previewUrl = null, tinyGifUrl = null, gifUrl = null, mediumGifUrl = null;

                if (item.TryGetProperty("images", out var images))
                {
                    // original → full GIF
                    gifUrl = GetImageUrl(images, "original");

                    // fixed_height → medium (cân bằng chất lượng và tốc độ)
                    mediumGifUrl = GetImageUrl(images, "fixed_height");

                    // fixed_height_small → tiny + preview
                    tinyGifUrl = GetImageUrl(images, "fixed_height_small");
                    previewUrl = GetImageUrl(images, "fixed_height_small");
                }

                if (!string.IsNullOrEmpty(id))
                    results.Add(new TenorGifDto(id, title, previewUrl, tinyGifUrl, gifUrl, mediumGifUrl));
            }
        }

        // Tính offset trang tiếp theo
        int? nextOffset = null;
        if (root.TryGetProperty("pagination", out var pagination))
        {
            var totalCount = pagination.TryGetProperty("total_count", out var tc) ? tc.GetInt32() : 0;
            var nextOff = currentOffset + limit;
            if (nextOff < totalCount)
                nextOffset = nextOff;
        }

        return new TenorSearchResult(results, nextOffset);
    }

    private static string? GetImageUrl(JsonElement images, string key)
    {
        if (images.TryGetProperty(key, out var img) &&
            img.TryGetProperty("url", out var u))
            return u.GetString();
        return null;
    }
}