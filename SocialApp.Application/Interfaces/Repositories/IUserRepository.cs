using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Contract cho User repository.
/// Kế thừa IGenericRepository để có sẵn CRUD cơ bản.
/// Chỉ định nghĩa thêm các query đặc thù cho User.
/// </summary>
public interface IUserRepository : IGenericRepository<User>
{
    /// <summary>
    /// Tìm user theo email (so sánh lowercase, đã trim).
    /// Trả null nếu không tìm thấy hoặc đã soft-delete.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Tìm user theo username (case-insensitive).
    /// Trả null nếu không tìm thấy hoặc đã soft-delete.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra email đã tồn tại trong hệ thống chưa (case-insensitive).
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Kiểm tra username đã tồn tại trong hệ thống chưa (case-insensitive).
    /// </summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
}