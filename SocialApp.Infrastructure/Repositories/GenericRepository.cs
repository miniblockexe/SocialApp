using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Common;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Common;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

/// <summary>
/// Implementation chung cho mọi repository.
/// Chỉ chứa truy vấn DB — không có business logic.
/// Mọi method đều async/await. Inject AppDbContext qua constructor.
/// </summary>
/// <typeparam name="T">Entity kế thừa BaseAuditableEntity.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : BaseAuditableEntity
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public GenericRepository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // Query — single

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        if (id == Guid.Empty) return null;

        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes)
    {
        if (id == Guid.Empty) return null;

        var query = ApplyIncludes(_dbSet.AsNoTracking(), includes);
        return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes)
    {
        var query = ApplyIncludes(_dbSet.AsNoTracking(), includes);
        return await query.FirstOrDefaultAsync(predicate, ct);
    }

    // Query — list

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt) // luôn OrderBy trước khi trả về
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<T>> GetAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(predicate)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<T>> GetAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes)
    {
        var query = ApplyIncludes(_dbSet.AsNoTracking(), includes);
        return await query
            .Where(predicate)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<T>> GetPagedAsync(
        PagedQuery query,
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken ct = default)
    {
        return await GetPagedAsync(query, predicate, orderBy, ct, []);
    }

    public async Task<PagedResult<T>> GetPagedAsync(
        PagedQuery query,
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes)
    {
        var source = ApplyIncludes(_dbSet.AsNoTracking(), includes);

        if (predicate is not null)
            source = source.Where(predicate);

        // Đếm trước khi skip/take
        var totalCount = await source.CountAsync(ct);

        // Luôn OrderBy trước Skip/Take
        var ordered = orderBy is not null
            ? query.SortAscending
                ? source.OrderBy(orderBy)
                : source.OrderByDescending(orderBy)
            : source.OrderByDescending(e => e.CreatedAt);

        var items = await ordered
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return PagedResult<T>.Create(items, totalCount, query.PageNumber, query.PageSize);
    }

    public IQueryable<T> Query() => _dbSet.AsNoTracking().AsQueryable();

    // Aggregate

    public async Task<int> CountAsync(CancellationToken ct = default)
        => await _dbSet.CountAsync(ct);

    public async Task<int> CountAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        => await _dbSet.CountAsync(predicate, ct);

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
        => await _dbSet.AnyAsync(predicate, ct);

    // Command

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    public async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default)
    {
        await _dbSet.AddRangeAsync(entities, ct);
    }

    public void Update(T entity)
    {
        // Attach nếu chưa được track, sau đó đánh dấu Modified
        _dbSet.Update(entity);
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    /// <summary>
    /// Soft-delete: AppDbContext.SaveChangesAsync sẽ intercept EntityState.Deleted
    /// và chuyển thành set DeletedAt = UtcNow thay vì xoá vật lý.
    /// </summary>
    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _context.SaveChangesAsync(ct);

    public async Task<int> ExecuteSoftDeleteAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default)
    {
        return await _dbSet
            .Where(predicate)
            .ExecuteUpdateAsync(s => s
                .SetProperty(e => e.DeletedAt, DateTime.UtcNow)
                .SetProperty(e => e.UpdatedAt, DateTime.UtcNow),
                ct);
    }

    // Private helpers

    /// <summary>
    /// Apply danh sách includes vào query. Không lazy load.
    /// </summary>
    private static IQueryable<T> ApplyIncludes(
        IQueryable<T> query,
        IEnumerable<Expression<Func<T, object>>> includes)
    {
        return includes.Aggregate(query, (current, include) => current.Include(include));
    }
}