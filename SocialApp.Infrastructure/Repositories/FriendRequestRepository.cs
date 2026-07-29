using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho FriendRequest.
/// FriendRequest không kế thừa BaseAuditableEntity nên không dùng GenericRepository.
/// </summary>
public sealed class FriendRequestRepository : IFriendRequestRepository
{
    private readonly AppDbContext _context;

    public FriendRequestRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        return await _context.FriendRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(fr => fr.Id == id, ct);
    }

    public async Task<FriendRequest?> GetBetweenUsersAsync(
        Guid userA, Guid userB, CancellationToken ct = default)
    {
        return await _context.FriendRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(fr =>
                (fr.SenderId == userA && fr.ReceiverId == userB) ||
                (fr.SenderId == userB && fr.ReceiverId == userA), ct);
    }

    public async Task<int> CountFriendsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.FriendRequests
            .CountAsync(fr =>
                fr.Status == FriendStatus.Accepted &&
                (fr.SenderId == userId || fr.ReceiverId == userId), ct);
    }

    public async Task<List<Guid>> GetFriendIdsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.FriendRequests
            .Where(fr =>
                fr.Status == FriendStatus.Accepted &&
                (fr.SenderId == userId || fr.ReceiverId == userId))
            .Select(fr => fr.SenderId == userId ? fr.ReceiverId : fr.SenderId)
            .ToListAsync(ct);
    }

    public async Task<List<Guid>> GetBlockedUserIdsAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.FriendRequests
            .Where(fr =>
                fr.Status == FriendStatus.Blocked &&
                (fr.SenderId == userId || fr.ReceiverId == userId))
            .Select(fr => fr.SenderId == userId ? fr.ReceiverId : fr.SenderId)
            .ToListAsync(ct);
    }

    public async Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct = default)
    {
        return await _context.FriendRequests
            .AnyAsync(fr =>
                fr.Status == FriendStatus.Accepted &&
                ((fr.SenderId == userA && fr.ReceiverId == userB) ||
                 (fr.SenderId == userB && fr.ReceiverId == userA)), ct);
    }

    public async Task<int> CountMutualFriendsAsync(
        Guid viewerId, Guid targetId, CancellationToken ct = default)
    {
        // Bạn của viewer
        var viewerFriendIds = await GetFriendIdsAsync(viewerId, ct);

        if (viewerFriendIds.Count == 0) return 0;

        // Đếm số bạn của target mà cũng nằm trong bạn của viewer
        return await _context.FriendRequests
            .CountAsync(fr =>
                fr.Status == FriendStatus.Accepted &&
                (fr.SenderId == targetId || fr.ReceiverId == targetId) &&
                viewerFriendIds.Contains(
                    fr.SenderId == targetId ? fr.ReceiverId : fr.SenderId), ct);
    }

    public async Task<Dictionary<Guid, int>> CountMutualFriendsBulkAsync(
        Guid viewerId, IEnumerable<Guid> targetIds, CancellationToken ct = default)
    {
        var targetIdList = targetIds.ToList();
        if (targetIdList.Count == 0) return new Dictionary<Guid, int>();

        // 1 query: lấy friendIds của viewer
        var viewerFriendIds = await GetFriendIdsAsync(viewerId, ct);
        if (viewerFriendIds.Count == 0)
            return targetIdList.ToDictionary(id => id, _ => 0);

        var viewerFriendSet = new HashSet<Guid>(viewerFriendIds);

        // 1 query: lấy tất cả accepted friendships liên quan đến targetIds
        var allFriendships = await _context.FriendRequests
            .Where(fr =>
                fr.Status == FriendStatus.Accepted &&
                (targetIdList.Contains(fr.SenderId) || targetIdList.Contains(fr.ReceiverId)))
            .Select(fr => new { fr.SenderId, fr.ReceiverId })
            .ToListAsync(ct);

        // Tính mutual in-memory
        var result = targetIdList.ToDictionary(id => id, _ => 0);
        foreach (var fr in allFriendships)
        {
            // Xác định targetId và friendId-của-target
            Guid targetId;
            Guid friendOfTarget;

            if (targetIdList.Contains(fr.SenderId) && fr.ReceiverId != viewerId)
            {
                targetId = fr.SenderId;
                friendOfTarget = fr.ReceiverId;
            }
            else if (targetIdList.Contains(fr.ReceiverId) && fr.SenderId != viewerId)
            {
                targetId = fr.ReceiverId;
                friendOfTarget = fr.SenderId;
            }
            else continue;

            if (viewerFriendSet.Contains(friendOfTarget))
                result[targetId]++;
        }

        return result;
    }

    public async Task<Dictionary<Guid, FriendRequest>> GetBetweenUsersBulkAsync(
        Guid viewerId, IEnumerable<Guid> targetIds, CancellationToken ct = default)
    {
        var targetIdList = targetIds.ToList();
        if (targetIdList.Count == 0) return new Dictionary<Guid, FriendRequest>();

        // 1 query cho tất cả
        var requests = await _context.FriendRequests
            .AsNoTracking()
            .Where(fr =>
                (fr.SenderId == viewerId && targetIdList.Contains(fr.ReceiverId)) ||
                (fr.ReceiverId == viewerId && targetIdList.Contains(fr.SenderId)))
            .ToListAsync(ct);

        // Map về targetId → FriendRequest
        var result = new Dictionary<Guid, FriendRequest>();
        foreach (var fr in requests)
        {
            var targetId = fr.SenderId == viewerId ? fr.ReceiverId : fr.SenderId;
            // Nếu có nhiều record (edge case), ưu tiên Accepted > Pending > khác
            if (!result.TryGetValue(targetId, out var existing) ||
                fr.Status == FriendStatus.Accepted)
            {
                result[targetId] = fr;
            }
        }
        return result;
    }

    public async Task<Dictionary<Guid, List<Guid>>> GetFriendIdsBulkAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        var userIdList = userIds.ToList();
        if (userIdList.Count == 0) return new Dictionary<Guid, List<Guid>>();

        // 1 query: tất cả accepted friendships liên quan đến userIds
        var friendships = await _context.FriendRequests
            .Where(fr =>
                fr.Status == FriendStatus.Accepted &&
                (userIdList.Contains(fr.SenderId) || userIdList.Contains(fr.ReceiverId)))
            .Select(fr => new { fr.SenderId, fr.ReceiverId })
            .ToListAsync(ct);

        var result = userIdList.ToDictionary(id => id, _ => new List<Guid>());
        foreach (var fr in friendships)
        {
            if (result.ContainsKey(fr.SenderId))
                result[fr.SenderId].Add(fr.ReceiverId);
            if (result.ContainsKey(fr.ReceiverId))
                result[fr.ReceiverId].Add(fr.SenderId);
        }
        return result;
    }

    public async Task AddAsync(FriendRequest entity, CancellationToken ct = default)
    {
        await _context.FriendRequests.AddAsync(entity, ct);
    }

    public void Update(FriendRequest entity)
    {
        _context.FriendRequests.Update(entity);
    }

    public void Remove(FriendRequest entity)
    {
        _context.FriendRequests.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public IQueryable<FriendRequest> Query()
    {
        return _context.FriendRequests.AsNoTracking();
    }
}