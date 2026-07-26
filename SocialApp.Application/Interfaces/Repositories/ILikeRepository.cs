using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Repository cho Like.
/// Like không kế thừa BaseAuditableEntity (không soft-delete) nên không dùng IGenericRepository&lt;T&gt;
/// được — cùng lý do FriendRequest có repo riêng.
/// </summary>
public interface ILikeRepository
{
    /// <summary>Tìm Like của userId cho postId cụ thể — null nếu chưa like. Dùng cho ToggleLikeAsync.</summary>
    Task<Like?> GetByUserAndPostAsync(Guid userId, Guid postId, CancellationToken ct = default);

    /// <summary>
    /// Lấy toàn bộ Like thuộc các postId được chỉ định trong 1 query — dùng để tính
    /// LikeCount + IsLikedByMe hàng loạt cho feed/danh sách bài viết (tránh N+1).
    /// </summary>
    Task<List<Like>> GetByPostIdsAsync(IEnumerable<Guid> postIds, CancellationToken ct = default);

    Task AddAsync(Like entity, CancellationToken ct = default);

    /// <summary>Xóa Like (unlike) — Like không có soft-delete nên đây là DELETE thật.</summary>
    void Remove(Like entity);

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}