using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho Post — extend GenericRepository, thêm AddMediaFilesAsync và share queries.
/// </summary>
public sealed class PostRepository : GenericRepository<Post>, IPostRepository
{
    private readonly AppDbContext _ctx;

    public PostRepository(AppDbContext context) : base(context)
    {
        _ctx = context;
    }

    /// <inheritdoc/>
    public async Task AddMediaFilesAsync(IEnumerable<PostMediaFile> files, CancellationToken ct = default)
    {
        await _ctx.PostMediaFiles.AddRangeAsync(files, ct);
        await _ctx.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task<Dictionary<Guid, int>> GetShareCountsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default)
    {
        var ids = postIds.ToList();
        if (ids.Count == 0) return [];

        return await _ctx.Posts
            .AsNoTracking()
            .Where(p => p.OriginalPostId != null
                     && ids.Contains(p.OriginalPostId.Value)
                     && p.DeletedAt == null)
            .GroupBy(p => p.OriginalPostId!.Value)
            .Select(g => new { OriginalPostId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OriginalPostId, x => x.Count, ct);
    }

    /// <inheritdoc/>
    public async Task<HashSet<Guid>> GetSharedPostIdsByUserAsync(
        Guid userId, IEnumerable<Guid> originalPostIds, CancellationToken ct = default)
    {
        var ids = originalPostIds.ToList();
        if (ids.Count == 0) return [];

        var result = await _ctx.Posts
            .AsNoTracking()
            .Where(p => p.UserId == userId
                     && p.OriginalPostId != null
                     && ids.Contains(p.OriginalPostId.Value)
                     && p.DeletedAt == null)
            .Select(p => p.OriginalPostId!.Value)
            .ToListAsync(ct);

        return [.. result];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Post>> GetOriginalPostsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default)
    {
        var ids = postIds.ToList();
        if (ids.Count == 0) return [];

        return await _ctx.Posts
            .AsNoTracking()
            .Include(p => p.User)
            .Include(p => p.PostMediaFiles)
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(ct);
    }
}