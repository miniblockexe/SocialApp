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
}