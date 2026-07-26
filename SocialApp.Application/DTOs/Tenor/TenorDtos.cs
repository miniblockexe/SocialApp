namespace SocialApp.Application.DTOs.Tenor;

/// <summary>
/// Kết quả từ Giphy search / trending.
/// Giữ namespace Tenor để không phải đổi các file khác đang import.
/// </summary>
public sealed record TenorSearchResult(
    IReadOnlyList<TenorGifDto> Results,
    /// <summary>Offset để load trang tiếp theo.</summary>
    int? NextOffset);

/// <summary>Một GIF item từ Giphy API.</summary>
public sealed record TenorGifDto(
    string Id,
    string Title,
    /// <summary>URL preview nhỏ (fixed_height_small) — dùng trong danh sách.</summary>
    string? PreviewUrl,
    /// <summary>URL GIF nhỏ (fixed_height_small) — nhẹ, dùng trong chat bubble.</summary>
    string? TinyGifUrl,
    /// <summary>URL full GIF (original).</summary>
    string? GifUrl,
    /// <summary>URL medium GIF (fixed_height).</summary>
    string? MediumGifUrl);