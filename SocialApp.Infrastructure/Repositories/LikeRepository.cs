using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Repository cho Like.
/// QUAN TRỌNG: GetByUserAndPostAsync KHÔNG dùng AsNoTracking
/// vì Remove() yêu cầu EF phải track entity — nếu AsNoTracking sẽ throw InvalidOperationException.
/// </summary>
public sealed class LikeRepository : ILikeRepository
{
    private readonly AppDbContext _context;

    public LikeRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lấy Like theo userId + postId — EF TRACK entity này (không AsNoTracking)
    /// để Remove() hoạt động đúng trong ToggleLike.
    /// </summary>
    public async Task<Like?> GetByUserAndPostAsync(
        Guid userId, Guid postId, CancellationToken ct = default)
    {
        return await _context.Likes
            // AsNoTracking bị xóa — cần tracking để Remove() hoạt động
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PostId == postId, ct);
    }

    /// <summary>
    /// Lấy danh sách Like theo nhiều postId — dùng AsNoTracking vì chỉ đọc, không cần Remove().
    /// </summary>
    public async Task<List<Like>> GetByPostIdsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default)
    {
        var ids = postIds.ToList();
        if (ids.Count == 0) return [];

        return await _context.Likes
            .AsNoTracking()   // OK — method này chỉ dùng để đọc, không Remove()
            .Where(l => ids.Contains(l.PostId))
            .ToListAsync(ct);
    }

    public async Task AddAsync(Like entity, CancellationToken ct = default)
    {
        await _context.Likes.AddAsync(entity, ct);
    }

    public void Remove(Like entity)
    {
        _context.Likes.Remove(entity);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}