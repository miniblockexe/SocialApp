using SocialApp.Application.Common;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Repository riêng cho Post — xử lý các thao tác không fit IGenericRepository,
/// đặc biệt là add PostMediaFile độc lập (không qua navigation property của Post)
/// để tránh DbUpdateConcurrencyException do HasDefaultValue trên Privacy.
/// </summary>
public interface IPostRepository : IGenericRepository<Post>
{
    /// <summary>
    /// Thêm danh sách PostMediaFile trực tiếp vào DbSet — không đụng đến Post entity.
    /// Tránh EF mark Post là Modified → sinh WHERE clause thừa → 0 rows affected.
    /// </summary>
    Task AddMediaFilesAsync(IEnumerable<PostMediaFile> files, CancellationToken ct = default);

    /// <summary>
    /// Đếm số lượt chia sẻ cho từng postId (1 query, tránh N+1).
    /// Key = originalPostId, Value = số bài đã chia sẻ lại bài đó (chưa bị xóa).
    /// </summary>
    Task<Dictionary<Guid, int>> GetShareCountsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default);

    /// <summary>
    /// Trả về tập originalPostId mà userId đã chia sẻ lại (trong danh sách đã cho).
    /// Dùng để tính IsSharedByMe theo kiểu bulk, tránh N+1.
    /// </summary>
    Task<HashSet<Guid>> GetSharedPostIdsByUserAsync(
        Guid userId, IEnumerable<Guid> originalPostIds, CancellationToken ct = default);

    /// <summary>
    /// Lấy các bài gốc theo danh sách Id (bao gồm cả đã soft-delete để hiển thị placeholder).
    /// Include User và PostMediaFiles để build OriginalPostDto.
    /// </summary>
    Task<IReadOnlyList<Post>> GetOriginalPostsAsync(
        IEnumerable<Guid> postIds, CancellationToken ct = default);

    /// <summary>
    /// Lấy feed chính với đầy đủ group logic — thay thế GetPagedAsync trong GetFeedAsync.
    /// Áp dụng các rule:
    ///   - Bài cá nhân (GroupId == null): hiện theo Privacy + friendIds như cũ.
    ///   - Bài trong Public group (GroupPostStatus == Approved): hiện cho tất cả.
    ///   - Bài trong Private group (GroupPostStatus == Approved): chỉ hiện nếu viewer là thành viên.
    ///   - Bài Pending/Rejected: không hiện trên feed dù là bất kỳ ai.
    /// </summary>
    /// <param name="userId">Viewer hiện tại.</param>
    /// <param name="friendIds">Danh sách bạn bè của viewer (để lọc Privacy.Friends).</param>
    /// <param name="blockedIds">Danh sách user bị block (ẩn bài của họ).</param>
    /// <param name="memberGroupIds">Danh sách groupId mà viewer đã là thành viên.</param>
    /// <param name="page">Trang (1-based).</param>
    /// <param name="size">Số item mỗi trang.</param>
    /// <param name="cursorCreatedAt">Cursor timestamp để infinite scroll (null = trang đầu).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<(IReadOnlyList<Post> Items, int TotalCount)> GetFeedPostsAsync(
        Guid userId,
        IReadOnlyCollection<Guid> friendIds,
        IReadOnlyCollection<Guid> blockedIds,
        IReadOnlyCollection<Guid> memberGroupIds,
        int page,
        int size,
        DateTime? cursorCreatedAt,
        CancellationToken ct = default);
}