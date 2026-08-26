using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho Post — extend GenericRepository, thêm AddMediaFilesAsync, share queries,
/// và GetFeedPostsAsync với đầy đủ group privacy logic.
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

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<Post> Items, int TotalCount)> GetFeedPostsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> friendIds,
        IReadOnlyCollection<Guid> blockedIds,
        IReadOnlyCollection<Guid> memberGroupIds,
        int page,
        int size,
        DateTime? cursorCreatedAt,
        CancellationToken ct = default)
    {
        var memberGroupIdSet = memberGroupIds.Select(id => (Guid?)id).ToHashSet();

        var query = _ctx.Posts
            .AsNoTracking()
            .Where(p =>
                p.DeletedAt == null &&
                !blockedIds.Contains(p.UserId) &&
                (cursorCreatedAt == null || p.CreatedAt < cursorCreatedAt));

        query = query.Where(p =>

            (p.GroupId == null &&
             (
                 p.UserId == userId ||
                 (friendIds.Contains(p.UserId) && p.Privacy != PostPrivacy.OnlyMe) ||
                 (p.UserId != userId && !friendIds.Contains(p.UserId) && p.Privacy == PostPrivacy.Public)
             ))

            ||

            (p.GroupId != null &&

             _ctx.GroupPosts.Any(gp =>
                 gp.PostId == p.Id &&
                 gp.Status == GroupPostStatus.Approved) &&

             (
                 _ctx.Groups.Any(g =>
                     g.Id == p.GroupId &&
                     g.Privacy == GroupPrivacy.Public) ||

                 memberGroupIdSet.Contains(p.GroupId)
             ))
        );

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(p => p.User)
            .Include(p => p.PostMediaFiles)
            .Include(p => p.Group)       // cần cho GroupName
            .Include(p => p.GroupPost)   // cần nếu service muốn đọc Status
            .ToListAsync(ct);

        return (items, totalCount);
    }
}