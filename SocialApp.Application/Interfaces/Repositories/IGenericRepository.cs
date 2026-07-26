using System.Linq.Expressions;
using SocialApp.Application.Common;
using SocialApp.Domain.Common;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Contract chung cho mọi repository. Chỉ chứa truy vấn DB — không có business logic.
/// Mọi method đều async. Inject qua constructor, không dùng [FromServices].
/// </summary>
/// <typeparam name="T">Entity kế thừa BaseAuditableEntity.</typeparam>
public interface IGenericRepository<T> where T : BaseAuditableEntity
{

    /// <summary>
    /// Lấy entity theo Id. Trả null nếu không tồn tại hoặc đã soft-delete.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lấy entity theo Id kèm các navigation property được chỉ định.
    /// Không lazy load — phải khai báo tường minh.
    /// </summary>
    Task<T?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Lấy entity đầu tiên thoả điều kiện. Trả null nếu không tìm thấy.
    /// </summary>
    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);

    /// <summary>
    /// Lấy entity đầu tiên thoả điều kiện kèm includes.
    /// </summary>
    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Lấy toàn bộ entity (áp dụng global query filter — đã loại soft-delete).
    /// Luôn OrderBy trước khi trả về.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Lấy danh sách theo điều kiện lọc.</summary>
    Task<IReadOnlyList<T>> GetAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);

    /// <summary>Lấy danh sách theo điều kiện lọc kèm includes.</summary>
    Task<IReadOnlyList<T>> GetAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Lấy danh sách có phân trang, filter, sort.
    /// Tự động clamp pageNumber/pageSize theo defensive coding rules.
    /// </summary>
    Task<PagedResult<T>> GetPagedAsync(
        PagedQuery query,
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken ct = default);

    /// <summary>Lấy danh sách có phân trang kèm includes.</summary>
    Task<PagedResult<T>> GetPagedAsync(
        PagedQuery query,
        Expression<Func<T, bool>>? predicate = null,
        Expression<Func<T, object>>? orderBy = null,
        CancellationToken ct = default,
        params Expression<Func<T, object>>[] includes);

    /// <summary>
    /// Trả về IQueryable để repository con compose thêm khi cần JOIN phức tạp.
    /// </summary>
    IQueryable<T> Query();

    /// <summary>Đếm tổng số bản ghi (sau global query filter).</summary>
    Task<int> CountAsync(CancellationToken ct = default);

    /// <summary>Đếm số bản ghi thoả điều kiện.</summary>
    Task<int> CountAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);

    /// <summary>Kiểm tra có ít nhất 1 bản ghi thoả điều kiện không.</summary>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);

    /// <summary>
    /// Thêm entity mới. Chưa SaveChanges — gọi SaveChangesAsync riêng.
    /// </summary>
    Task AddAsync(T entity, CancellationToken ct = default);

    /// <summary>Thêm nhiều entity cùng lúc (bulk insert).</summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken ct = default);

    /// <summary>Cập nhật entity. Chưa SaveChanges.</summary>
    void Update(T entity);

    /// <summary>Cập nhật nhiều entity cùng lúc.</summary>
    void UpdateRange(IEnumerable<T> entities);

    /// <summary>
    /// Soft-delete: AppDbContext intercept EntityState.Deleted → set DeletedAt = UtcNow.
    /// Không xoá vật lý khỏi DB.
    /// </summary>
    void Remove(T entity);

    /// <summary>Soft-delete nhiều entity.</summary>
    void RemoveRange(IEnumerable<T> entities);

    /// <summary>Lưu toàn bộ thay đổi xuống DB. Trả số bản ghi bị ảnh hưởng.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Bulk soft-delete theo predicate: UPDATE SET DeletedAt=UtcNow, UpdatedAt=UtcNow WHERE ...
    /// Dùng ExecuteUpdateAsync — không qua change tracker, tránh DbUpdateConcurrencyException.
    /// </summary>
    Task<int> ExecuteSoftDeleteAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken ct = default);
}