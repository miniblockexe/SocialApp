using System.Diagnostics;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SocialApp.Application.Common;
using SocialApp.Application.Common.Exceptions;
using SocialApp.Application.DTOs.Admin;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Cloud;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Application.Interfaces.Services;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Services;

/// <summary>
/// Triển khai IAdminService — toàn bộ admin action đều có audit log (Warning level).
/// Cache dashboard 5 phút, cloud stats 10 phút.
/// </summary>
public sealed class AdminService : IAdminService
{
    private const string DashboardCacheKey = "admin:dashboard";
    private const string CloudStatsCacheKey = "admin:cloud_stats";
    private static readonly TimeSpan DashboardCacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CloudStatsCacheDuration = TimeSpan.FromMinutes(10);

    private readonly IAdminDbContext _db;
    private readonly ICloudService _cloudService;
    private readonly IMemoryCache _cache;
    private readonly IMapper _mapper;
    private readonly ILogger<AdminService> _logger;

    public AdminService(
        IAdminDbContext db,
        ICloudService cloudService,
        IMemoryCache cache,
        IMapper mapper,
        ILogger<AdminService> logger)
    {
        _db = db;
        _cloudService = cloudService;
        _cache = cache;
        _mapper = mapper;
        _logger = logger;
    }

    // =========================================================================
    // Dashboard
    // =========================================================================

    public async Task<AdminDashboardDto> GetDashboardStatsAsync()
    {
        if (_cache.TryGetValue(DashboardCacheKey, out AdminDashboardDto? cached) && cached is not null)
            return cached;

        var sw = Stopwatch.StartNew();
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var todayUtc = now.Date;

        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.LastSeen >= sevenDaysAgo);
        var newUsersToday = await _db.Users.CountAsync(u => u.CreatedAt >= todayUtc);
        var bannedUsers = await _db.Users.CountAsync(u => u.IsBanned);

        var totalPosts = await _db.Posts.IgnoreQueryFilters().CountAsync();
        var activePosts = await _db.Posts.CountAsync();
        var deletedPosts = await _db.Posts.IgnoreQueryFilters().CountAsync(p => p.DeletedAt != null);
        var postsToday = await _db.Posts.IgnoreQueryFilters().CountAsync(p => p.CreatedAt >= todayUtc);

        var totalMessages = await _db.Messages.CountAsync();
        var messagesToday = await _db.Messages.CountAsync(m => m.CreatedAt >= todayUtc);
        var totalComments = await _db.Comments.IgnoreQueryFilters().CountAsync();
        var totalLikes = await _db.Likes.CountAsync();
        var totalFriendships = await _db.FriendRequests.CountAsync(f => f.Status == FriendStatus.Accepted);

        sw.Stop();
        _logger.LogInformation(
            "AdminService.GetDashboardStatsAsync completed in {ElapsedMs}ms", sw.ElapsedMilliseconds);

        var dto = new AdminDashboardDto
        {
            TotalUsers = totalUsers,
            ActiveUsersLast7Days = activeUsers,
            NewUsersToday = newUsersToday,
            BannedUsers = bannedUsers,
            TotalPosts = totalPosts,
            ActivePosts = activePosts,
            DeletedPosts = deletedPosts,
            PostsToday = postsToday,
            TotalMessages = totalMessages,
            MessagesToday = messagesToday,
            TotalComments = totalComments,
            TotalLikes = totalLikes,
            TotalFriendships = totalFriendships,
            GeneratedAt = now
        };

        _cache.Set(DashboardCacheKey, dto, DashboardCacheDuration);
        return dto;
    }

    // =========================================================================
    // Posts
    // =========================================================================

    public async Task<PagedResult<AdminPostDto>> GetAllPostsAsync(AdminPostQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var size = query.Size < 1 ? 10 : query.Size > 100 ? 100 : query.Size;

        if (query.FromDate.HasValue && query.ToDate.HasValue && query.FromDate >= query.ToDate)
            throw new ArgumentException("FromDate phải nhỏ hơn ToDate.");

        var validSortFields = new[] { "createdAt", "likeCount", "commentCount" };
        var sortBy = validSortFields.Contains(query.SortBy?.ToLower())
            ? query.SortBy!.ToLower()
            : "createdAt";

        // IgnoreQueryFilters để admin thấy cả bài đã xóa
        var q = _db.Posts
            .IgnoreQueryFilters()
            .Include(p => p.User)
            .Include(p => p.PostMediaFiles)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .AsQueryable();

        if (query.IsDeleted.HasValue)
            q = query.IsDeleted.Value
                ? q.Where(p => p.DeletedAt != null)
                : q.Where(p => p.DeletedAt == null);

        if (query.UserId.HasValue && query.UserId.Value != Guid.Empty)
            q = q.Where(p => p.UserId == query.UserId.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(p => p.Content != null && p.Content.ToLower().Contains(kw));
        }

        if (query.FromDate.HasValue)
            q = q.Where(p => p.CreatedAt >= query.FromDate.Value);

        if (query.ToDate.HasValue)
            q = q.Where(p => p.CreatedAt <= query.ToDate.Value);

        q = sortBy switch
        {
            "likeCount" => query.SortDesc
                ? q.OrderByDescending(p => p.Likes.Count)
                : q.OrderBy(p => p.Likes.Count),
            "commentCount" => query.SortDesc
                ? q.OrderByDescending(p => p.Comments.Count(c => c.DeletedAt == null))
                : q.OrderBy(p => p.Comments.Count(c => c.DeletedAt == null)),
            _ => query.SortDesc
                ? q.OrderByDescending(p => p.CreatedAt)
                : q.OrderBy(p => p.CreatedAt)
        };

        var totalCount = await q.CountAsync();
        var posts = await q
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = posts.Select(p => new AdminPostDto
        {
            Id = p.Id,
            Content = p.Content,
            Privacy = p.Privacy,
            IsDeleted = p.DeletedAt.HasValue,
            DeletedAt = p.DeletedAt,
            DeletedByAdmin = false,
            AdminDeleteReason = null,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt,
            Author = _mapper.Map<UserBriefDto>(p.User),
            MediaCount = p.PostMediaFiles.Count,
            LikeCount = p.Likes.Count,
            CommentCount = p.Comments.Count(c => c.DeletedAt == null)
        }).ToList();

        return PagedResult<AdminPostDto>.Create(items, totalCount, page, size);
    }

    public async Task AdminDeletePostAsync(Guid adminId, Guid postId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Lý do xóa không được để trống.");

        var post = await _db.Posts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == postId)
            ?? throw new KeyNotFoundException($"Không tìm thấy bài đăng với Id: {postId}");

        if (post.DeletedAt.HasValue)
            throw new InvalidOperationException("Bài viết đã được xóa trước đó.");

        var now = DateTime.UtcNow;
        post.DeletedAt = now;
        post.UpdatedAt = now;
        await _db.SaveChangesAsync();

        _logger.LogWarning(
            "ADMIN_ACTION: Admin {AdminId} deleted post {PostId}. Reason: {Reason}",
            adminId, postId, reason.Trim());

        InvalidateDashboardCache();
    }

    public async Task AdminRestorePostAsync(Guid adminId, Guid postId)
    {
        var post = await _db.Posts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == postId)
            ?? throw new KeyNotFoundException($"Không tìm thấy bài đăng với Id: {postId}");

        if (!post.DeletedAt.HasValue)
            throw new InvalidOperationException("Bài viết chưa bị xóa.");

        post.DeletedAt = null;
        post.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogWarning(
            "ADMIN_ACTION: Admin {AdminId} restored post {PostId}",
            adminId, postId);
    }

    // =========================================================================
    // Users
    // =========================================================================

    public async Task<PagedResult<AdminUserDto>> GetAllUsersAsync(AdminUserQueryDto query)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var size = query.Size < 1 ? 10 : query.Size > 100 ? 100 : query.Size;

        var validSortFields = new[] { "createdAt", "lastSeen", "postCount" };
        var sortBy = validSortFields.Contains(query.SortBy?.ToLower())
            ? query.SortBy!.ToLower()
            : "createdAt";

        var q = _db.Users
            .Include(u => u.Posts)
            .Include(u => u.SentFriendRequests)
            .Include(u => u.ReceivedFriendRequests)
            .Include(u => u.SentMessages)
            .AsQueryable();

        if (query.IsBanned.HasValue)
            q = q.Where(u => u.IsBanned == query.IsBanned.Value);

        if (query.Role.HasValue)
            q = q.Where(u => u.Role == query.Role.Value);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim().ToLower();
            q = q.Where(u => u.Username.ToLower().Contains(kw) || u.Email.ToLower().Contains(kw));
        }

        q = sortBy switch
        {
            "lastSeen" => query.SortDesc
                ? q.OrderByDescending(u => u.LastSeen)
                : q.OrderBy(u => u.LastSeen),
            "postCount" => query.SortDesc
                ? q.OrderByDescending(u => u.Posts.Count)
                : q.OrderBy(u => u.Posts.Count),
            _ => query.SortDesc
                ? q.OrderByDescending(u => u.CreatedAt)
                : q.OrderBy(u => u.CreatedAt)
        };

        var totalCount = await q.CountAsync();
        var users = await q
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        var items = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Username = u.Username,
            Email = u.Email,
            FullName = u.FullName,
            AvatarUrl = u.AvatarUrl,
            Role = u.Role,
            IsActive = u.IsActive,
            IsBanned = u.IsBanned,
            BannedReason = u.BannedReason,
            CreatedAt = u.CreatedAt,
            LastSeen = u.LastSeen,
            PostCount = u.Posts.Count(p => p.DeletedAt == null),
            FriendCount = u.SentFriendRequests.Count(f => f.Status == FriendStatus.Accepted)
                         + u.ReceivedFriendRequests.Count(f => f.Status == FriendStatus.Accepted),
            MessageCount = u.SentMessages.Count
            // PasswordHash KHÔNG được map trong bất kỳ trường hợp nào
        }).ToList();

        return PagedResult<AdminUserDto>.Create(items, totalCount, page, size);
    }

    public async Task BanUserAsync(Guid adminId, Guid targetUserId, string reason)
    {
        if (adminId == targetUserId)
            throw new ArgumentException("Không thể tự cấm tài khoản của mình.");

        var user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"Không tìm thấy user với Id: {targetUserId}");

        if (user.Role == UserRole.Admin)
            throw new UnauthorizedAccessException("Không thể cấm tài khoản Admin khác.");

        if (user.IsBanned)
            throw new InvalidOperationException("Tài khoản đã bị cấm trước đó.");

        var now = DateTime.UtcNow;

        user.IsBanned = true;
        user.BannedReason = reason.Trim();

        // Revoke TẤT CẢ refresh token — force logout mọi thiết bị
        foreach (var token in user.RefreshTokens.Where(t => !t.IsRevoked))
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }

        // Tạo system notification cho user bị ban
        _db.Notifications.Add(new Notification
        {
            UserId = targetUserId,
            ActorId = adminId,
            Type = NotificationType.System,
            Content = $"Tài khoản của bạn đã bị khóa. Lý do: {reason.Trim()}",
            CreatedAt = now
        });

        await _db.SaveChangesAsync();

        // Xóa cache BannedUser middleware — key phải khớp BanStatusChecker: "ban_{userId}"
        _cache.Remove($"ban_{targetUserId}");

        _logger.LogWarning(
            "ADMIN_ACTION: Admin {AdminId} banned user {TargetUserId}. Reason: {Reason}",
            adminId, targetUserId, reason.Trim());

        InvalidateDashboardCache();
    }

    public async Task UnbanUserAsync(Guid adminId, Guid targetUserId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException($"Không tìm thấy user với Id: {targetUserId}");

        if (!user.IsBanned)
            throw new InvalidOperationException("Tài khoản không bị cấm.");

        user.IsBanned = false;
        user.BannedReason = null;

        _db.Notifications.Add(new Notification
        {
            UserId = targetUserId,
            ActorId = adminId,
            Type = NotificationType.System,
            Content = "Tài khoản của bạn đã được mở khóa.",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        // Xóa cache BannedUser middleware — key phải khớp BanStatusChecker: "ban_{userId}"
        _cache.Remove($"ban_{targetUserId}");

        _logger.LogWarning(
            "ADMIN_ACTION: Admin {AdminId} unbanned user {TargetUserId}",
            adminId, targetUserId);
    }

    // =========================================================================
    // Cloud
    // =========================================================================

    public async Task<AdminCloudStatsDto> GetCloudStatsAsync()
    {
        if (_cache.TryGetValue(CloudStatsCacheKey, out AdminCloudStatsDto? cached) && cached is not null)
            return cached;

        try
        {
            var stats = await _cloudService.GetStatsAsync();
            _cache.Set(CloudStatsCacheKey, stats, CloudStatsCacheDuration);
            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AdminService.GetCloudStatsAsync: Lỗi khi lấy cloud stats");
            throw new InvalidOperationException("Không thể lấy thống kê cloud storage. Vui lòng thử lại sau.");
        }
    }

    public async Task AdminDeleteCloudFileAsync(Guid adminId, AdminDeleteCloudFileDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.PublicIdOrKey))
            throw new ArgumentException("PublicIdOrKey không được để trống.");

        await _cloudService.DeleteMediaAsync(dto.PublicIdOrKey, dto.Provider, dto.MediaType);

        if (dto.PostMediaFileId.HasValue && dto.PostMediaFileId.Value != Guid.Empty)
        {
            var mediaFile = await _db.PostMediaFiles
                .FirstOrDefaultAsync(f => f.Id == dto.PostMediaFileId.Value);

            if (mediaFile is not null)
            {
                _db.PostMediaFiles.Remove(mediaFile);
                await _db.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning(
                    "AdminService.AdminDeleteCloudFileAsync: PostMediaFile {Id} không tìm thấy trong DB, bỏ qua.",
                    dto.PostMediaFileId.Value);
            }
        }

        _logger.LogWarning(
            "ADMIN_ACTION: Admin {AdminId} deleted cloud file {Key} from {Provider}",
            adminId, dto.PublicIdOrKey, dto.Provider);

        _cache.Remove(CloudStatsCacheKey);
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private void InvalidateDashboardCache() => _cache.Remove(DashboardCacheKey);
}