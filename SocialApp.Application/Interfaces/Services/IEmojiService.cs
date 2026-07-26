using SocialApp.Application.DTOs.Emoji;

namespace SocialApp.Application.Interfaces.Services;

/// <summary>
/// Lấy danh sách emoji cho picker trong chat (EmojiHub API — free, no key).
/// Kết quả được cache 24h để giảm request ra ngoài.
/// </summary>
public interface IEmojiService
{
    Task<IReadOnlyList<EmojiDto>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<EmojiDto>> GetByCategoryAsync(string category, CancellationToken ct = default);
}
