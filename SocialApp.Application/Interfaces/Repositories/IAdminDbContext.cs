using Microsoft.EntityFrameworkCore;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Interfaces.Repositories;

/// <summary>
/// Interface cho AdminService — cung cấp đầy đủ DbSet cần thiết cho admin operations.
/// Tuân thủ Dependency Inversion: Application không reference Infrastructure trực tiếp.
/// AppDbContext implement interface này.
/// </summary>
public interface IAdminDbContext
{
    DbSet<User> Users { get; }
    DbSet<Post> Posts { get; }
    DbSet<PostMediaFile> PostMediaFiles { get; }
    DbSet<Comment> Comments { get; }
    DbSet<Like> Likes { get; }
    DbSet<Message> Messages { get; }
    DbSet<FriendRequest> FriendRequests { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<RefreshToken> RefreshTokens { get; }

    /// <summary>Lưu toàn bộ thay đổi xuống DB.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}