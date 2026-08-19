using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Repositories;

public interface IGroupRepository
{
    // ── Group ──────────────────────────────────────────────────────────
    Task<Group?> GetByIdAsync(Guid id, bool includeMembers = false, CancellationToken ct = default);
    Task AddAsync(Group group, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    // ── Member ─────────────────────────────────────────────────────────
    Task<GroupMember?> GetMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task AddMemberAsync(GroupMember member, CancellationToken ct = default);
    Task RemoveMemberAsync(GroupMember member);
    Task<int> GetMemberCountAsync(Guid groupId, CancellationToken ct = default);
    Task<List<GroupMember>> GetMembersPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<GroupRole?> GetRoleAsync(Guid groupId, Guid userId, CancellationToken ct = default);

    // ── Join Request ───────────────────────────────────────────────────
    Task<GroupJoinRequest?> GetJoinRequestAsync(Guid groupId, Guid userId, CancellationToken ct = default);
    Task<GroupJoinRequest?> GetJoinRequestByIdAsync(Guid requestId, CancellationToken ct = default);
    Task AddJoinRequestAsync(GroupJoinRequest request, CancellationToken ct = default);
    Task<List<GroupJoinRequest>> GetPendingRequestsPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default);
    Task<int> GetPendingRequestCountAsync(Guid groupId, CancellationToken ct = default);

    // ── Group Post ─────────────────────────────────────────────────────
    Task<GroupPost?> GetGroupPostAsync(Guid postId, Guid groupId, CancellationToken ct = default);
    Task AddGroupPostAsync(GroupPost groupPost, CancellationToken ct = default);
    Task<List<Post>> GetGroupFeedAsync(Guid groupId, int size, Guid? cursorId, CancellationToken ct = default);
    Task<List<Post>> GetPendingPostsPagedAsync(Guid groupId, int page, int size, CancellationToken ct = default);

    // ── Search ─────────────────────────────────────────────────────────
    Task<(List<Group> Items, int TotalCount)> SearchGroupsAsync(string? keyword, int page, int size, CancellationToken ct = default);
    Task<(List<Group> Items, int TotalCount)> GetUserGroupsAsync(Guid userId, int page, int size, CancellationToken ct = default);
    Task<HashSet<Guid>> GetUserGroupIdsAsync(Guid userId, CancellationToken ct = default);
}
