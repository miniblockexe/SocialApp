using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho Notification — mở rộng đầy đủ cho Notification module.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;

    public NotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Notification entity, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(entity, ct);
    }

    public async Task<bool> ExistsDuplicateAsync(
        Guid recipientId,
        Guid actorId,
        int notificationType,
        Guid? entityId,
        DateTime since,
        CancellationToken ct = default)
    {
        return await _context.Notifications
            .AnyAsync(n =>
                n.UserId == recipientId &&
                n.ActorId == actorId &&
                (int)n.Type == notificationType &&
                n.EntityId == entityId &&
                n.CreatedAt >= since, ct);
    }

    public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;
        return await _context.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int skip, int take, CancellationToken ct = default)
    {
        var query = _context.Notifications
            .Include(n => n.Actor)
            .Where(n => n.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);
    }

    public async Task<int> CountTotalAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .CountAsync(n => n.UserId == userId, ct);
    }

    public async Task<List<Notification>> GetByIdsAndUserAsync(
        Guid userId, List<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];

        return await _context.Notifications
            .Where(n => n.UserId == userId && ids.Contains(n.Id) && !n.IsRead)
            .ToListAsync(ct);
    }

    public async Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true), ct);
    }

    public void Remove(Notification entity)
    {
        _context.Notifications.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}