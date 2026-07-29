using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Repository riêng cho FriendRequest.
/// FriendRequest không kế thừa BaseAuditableEntity nên không dùng IGenericRepository được.
/// </summary>
public interface IFriendRequestRepository
{
    /// <summary>Tìm record theo Id. Trả null nếu không tồn tại.</summary>
    Task<FriendRequest?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Tìm record quan hệ giữa 2 user theo cả 2 chiều.
    /// </summary>
    Task<FriendRequest?> GetBetweenUsersAsync(Guid userA, Guid userB, CancellationToken ct = default);

    /// <summary>
    /// Đếm số bạn bè (status = Accepted) của một user.
    /// </summary>
    Task<int> CountFriendsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách friendId (status = Accepted) của một user.
    /// </summary>
    Task<List<Guid>> GetFriendIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Lấy danh sách userId bị block hoặc đã block (cả 2 chiều).
    /// </summary>
    Task<List<Guid>> GetBlockedUserIdsAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra 2 user có phải bạn bè không.
    /// </summary>
    Task<bool> AreFriendsAsync(Guid userA, Guid userB, CancellationToken ct = default);

    /// <summary>
    /// Tính số bạn chung giữa viewer và target.
    /// </summary>
    Task<int> CountMutualFriendsAsync(Guid viewerId, Guid targetId, CancellationToken ct = default);

    /// <summary>
    /// Bulk: đếm bạn chung giữa viewer và nhiều target cùng lúc
    /// </summary>
    Task<Dictionary<Guid, int>> CountMutualFriendsBulkAsync(
        Guid viewerId, IEnumerable<Guid> targetIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk: lấy FriendRequest giữa viewer và nhiều user cùng lúc
    /// </summary>
    Task<Dictionary<Guid, FriendRequest>> GetBetweenUsersBulkAsync(
        Guid viewerId, IEnumerable<Guid> targetIds, CancellationToken ct = default);

    /// <summary>
    /// Bulk: lấy friendIds của nhiều user cùng lúc
    /// Key = userId, Value = list friendIds của user đó.
    /// </summary>
    Task<Dictionary<Guid, List<Guid>>> GetFriendIdsBulkAsync(
        IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// <summary>Thêm record mới.</summary>
    Task AddAsync(FriendRequest entity, CancellationToken ct = default);

    /// <summary>Cập nhật record.</summary>
    void Update(FriendRequest entity);

    /// <summary>Xóa record (hard delete — FriendRequest không soft delete).</summary>
    void Remove(FriendRequest entity);

    /// <summary>Lưu thay đổi xuống DB.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Trả IQueryable để service compose thêm query khi cần.
    /// </summary>
    IQueryable<FriendRequest> Query();
}