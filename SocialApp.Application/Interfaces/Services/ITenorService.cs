using SocialApp.Application.DTOs.Tenor;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Tìm kiếm và lấy trending GIF từ Tenor (free 10k req/ngày).
/// </summary>
public interface ITenorService
{
    Task<TenorSearchResult> SearchAsync(string query, int limit = 20, string? pos = null, CancellationToken ct = default);
    Task<TenorSearchResult> TrendingAsync(int limit = 20, CancellationToken ct = default);
}
