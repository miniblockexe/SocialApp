using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Services;

/// <summary>
/// Kiểm tra user có bị ban không.
/// Kết quả được cache 5 phút trong IMemoryCache để tránh query DB mỗi request.
/// Cache bị xóa ngay khi AdminService ban/unban user.
/// </summary>
public sealed class BanStatusChecker : IBanStatusChecker
{
    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<BanStatusChecker> _logger;

    // Cache key: "ban_{userId}" — phải khớp với key mà AdminService dùng để invalidate
    private static string CacheKey(Guid userId) => $"ban_{userId}";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public BanStatusChecker(
        AppDbContext context,
        IMemoryCache cache,
        ILogger<BanStatusChecker> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<bool> IsUserBannedAsync(Guid userId, CancellationToken ct = default)
    {
        if (userId == Guid.Empty) return false;

        var key = CacheKey(userId);

        // Trả cache nếu đã có
        if (_cache.TryGetValue(key, out bool cachedResult))
            return cachedResult;

        // Query DB
        var isBanned = await _context.Users
            .AnyAsync(u => u.Id == userId && u.IsBanned, ct);

        // Lưu vào cache
        _cache.Set(key, isBanned, CacheDuration);

        if (isBanned)
            _logger.LogWarning(
                "BanStatusChecker: banned user attempted access — UserId={UserId}", userId);

        return isBanned;
    }
}