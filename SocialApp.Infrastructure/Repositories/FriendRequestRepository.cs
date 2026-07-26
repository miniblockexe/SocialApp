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