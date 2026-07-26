using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Repository cho Notification. Notification không kế thừa BaseAuditableEntity nên không dùng
/// IGenericRepository&lt;T&gt;.
/// Mở rộng đầy đủ cho Notification module: tạo, query, mark-read, delete, count.
/// </summary>
public interface INotificationRepository
{
    /// <summary>Thêm notification mới. Chưa SaveChanges.</summary>
    Task AddAsync(Notification entity, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra duplicate notification trong khoảng thời gian chỉ định.
    /// Dùng để tránh spam notification (like/unlike nhanh).
    /// </summary>
    Task<bool> ExistsDuplicateAsync(
        Guid recipientId,
        Guid actorId,
        int notificationType,
        Guid? entityId,
        DateTime since,
        CancellationToken ct = default);

    /// <summary>Lấy notification theo Id. Trả null nếu không tồn tại.</summary>
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách notification của user, kèm Actor.
    /// OrderBy CreatedAt DESC, phân trang.
    /// </summary>
    Task<(List<Notification> Items, int TotalCount)> GetPagedAsync(
        Guid userId, int skip, int take, CancellationToken ct = default);

    /// <summary>Đếm số notification chưa đọc của user.</summary>
    Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Đếm tổng số notification của user.</summary>
    Task<int> CountTotalAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy các notification theo danh sách Id, lọc theo userId.
    /// Dùng cho MarkAsReadAsync — chỉ lấy notification thuộc về user.
    /// </summary>
    Task<List<Notification>> GetByIdsAndUserAsync(
        Guid userId, List<Guid> ids, CancellationToken ct = default);

    /// <summary>
    /// Đánh dấu tất cả notification chưa đọc của user là đã đọc (1 query ExecuteUpdate).
    /// Trả số bản ghi bị ảnh hưởng.
    /// </summary>
    Task<int> MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Xóa notification (hard delete). Chưa SaveChanges.</summary>
    void Remove(Notification entity);

    /// <summary>Lưu thay đổi xuống DB.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}