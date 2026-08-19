using Microsoft.EntityFrameworkCore;
using SocialApp.Application.Interfaces.Repositories;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;
using SocialApp.Infrastructure.Data;

namespace SocialApp.Infrastructure.Repositories;

public sealed class GroupRepository : IGroupRepository
{
    private readonly AppDbContext _db;
    public GroupRepository(AppDbContext db) => _db = db;

    // ── Group ──────────────────────────────────────────────────────────

    public async Task<Group?> GetByIdAsync(Guid id, bool includeMembers = false, CancellationToken ct = default)
    {
        var q = _db.Groups.Include(g => g.Owner).AsQueryable();
        if (includeMembers) q = q.Include(g => g.Members).ThenInclude(m => m.User);
        return await q.FirstOrDefaultAsync(g => g.Id == id && g.DeletedAt == null, ct);
    }

    public async Task AddAsync(Group group, CancellationToken ct = default)
        => await _db.Groups.AddAsync(group, ct);

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
        => await _db.SaveChangesAsync(ct);

    // ── Member ─────────────────────────────────────────────────────────

    public async Task<GroupMember?> GetMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        => await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId, ct);

    public async Task AddMemberAsync(GroupMember member, CancellationToken ct = default)
        => await _db.GroupMembers.AddAsync(member, ct);

    public Task RemoveMemberAsync(GroupMember member)
    {
        _db.GroupMembers.Remove(member);
        return Task.CompletedTask;
    }

    public async Task<int> GetMemberCountAsync(Guid groupId, CancellationToken ct = default)
        => await _db.GroupMembers.CountAsync(m => m.GroupId == groupId, ct);

    public async Task<List<GroupMember>> GetMembersPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default)
        => await _db.GroupMembers
            .Where(m => m.GroupId == groupId)
            .Include(m => m.User)
            .OrderByDescending(m => m.Role)
            .ThenBy(m => m.JoinedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

    public async Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        => await _db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId, ct);

    public async Task<GroupRole?> GetRoleAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        => await _db.GroupMembers
            .Where(m => m.GroupId == groupId && m.UserId == userId)
            .Select(m => (GroupRole?)m.Role)
            .FirstOrDefaultAsync(ct);

    // ── Join Request ───────────────────────────────────────────────────

    public async Task<GroupJoinRequest?> GetJoinRequestAsync(Guid groupId, Guid userId, CancellationToken ct = default)
        => await _db.GroupJoinRequests
            .Where(r => r.GroupId == groupId && r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<GroupJoinRequest?> GetJoinRequestByIdAsync(Guid requestId, CancellationToken ct = default)
        => await _db.GroupJoinRequests
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == requestId, ct);

    public async Task AddJoinRequestAsync(GroupJoinRequest request, CancellationToken ct = default)
        => await _db.GroupJoinRequests.AddAsync(request, ct);

    public async Task<List<GroupJoinRequest>> GetPendingRequestsPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default)
        => await _db.GroupJoinRequests
            .Where(r => r.GroupId == groupId && r.Status == JoinRequestStatus.Pending)
            .Include(r => r.User)
            .OrderBy(r => r.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

    public async Task<int> GetPendingRequestCountAsync(Guid groupId, CancellationToken ct = default)
        => await _db.GroupJoinRequests.CountAsync(r => r.GroupId == groupId && r.Status == JoinRequestStatus.Pending, ct);

    // ── Group Post ─────────────────────────────────────────────────────

    public async Task<GroupPost?> GetGroupPostAsync(Guid postId, Guid groupId, CancellationToken ct = default)
        => await _db.GroupPosts
            .Include(gp => gp.Post)
            .FirstOrDefaultAsync(gp => gp.PostId == postId && gp.GroupId == groupId, ct);

    public async Task AddGroupPostAsync(GroupPost groupPost, CancellationToken ct = default)
        => await _db.GroupPosts.AddAsync(groupPost, ct);

    public async Task<List<Post>> GetGroupFeedAsync(Guid groupId, int size, Guid? cursorId, CancellationToken ct = default)
    {
        var q = _db.Posts
            .Where(p => p.GroupId == groupId
                     && p.GroupPost != null
                     && p.GroupPost.Status == GroupPostStatus.Approved
                     && p.DeletedAt == null)
            .Include(p => p.User)
            .Include(p => p.PostMediaFiles)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Include(p => p.GroupPost)
            .AsQueryable();

        if (cursorId.HasValue)
        {
            var cursorDate = await _db.Posts
                .Where(p => p.Id == cursorId.Value)
                .Select(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);
            if (cursorDate != default)
                q = q.Where(p => p.CreatedAt < cursorDate);
        }

        return await q.OrderByDescending(p => p.CreatedAt).Take(size).ToListAsync(ct);
    }

    public async Task<List<Post>> GetPendingPostsPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default)
        => await _db.Posts
            .Where(p => p.GroupId == groupId
                     && p.GroupPost != null
                     && p.GroupPost.Status == GroupPostStatus.Pending
                     && p.DeletedAt == null)
            .Include(p => p.User)
            .Include(p => p.PostMediaFiles)
            .Include(p => p.Likes)
            .Include(p => p.Comments)
            .Include(p => p.GroupPost)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

    // ── Search ─────────────────────────────────────────────────────────

    public async Task<(List<Group> Items, int TotalCount)> SearchGroupsAsync(string? keyword, int page, int size, CancellationToken ct = default)
    {
        var q = _db.Groups.Include(g => g.Owner).Where(g => g.DeletedAt == null).AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var kw = keyword.Trim().ToLower();
            q = q.Where(g => g.Name.ToLower().Contains(kw) || (g.Description != null && g.Description.ToLower().Contains(kw)));
        }
        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(g => g.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (items, total);
    }

    public async Task<(List<Group> Items, int TotalCount)> GetUserGroupsAsync(Guid userId, int page, int size, CancellationToken ct = default)
    {
        var q = _db.GroupMembers
            .Where(m => m.UserId == userId)
            .Include(m => m.Group).ThenInclude(g => g.Owner)
            .Where(m => m.Group.DeletedAt == null)
            .Select(m => m.Group);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(g => g.CreatedAt).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return (items!, total);
    }

    public async Task<HashSet<Guid>> GetUserGroupIdsAsync(Guid userId, CancellationToken ct = default)
    {
        var ids = await _db.GroupMembers.Where(m => m.UserId == userId).Select(m => m.GroupId).ToListAsync(ct);
        return ids.ToHashSet();
    }
}
