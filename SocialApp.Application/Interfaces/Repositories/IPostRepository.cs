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
}