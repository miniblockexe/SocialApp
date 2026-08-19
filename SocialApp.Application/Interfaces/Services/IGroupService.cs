using SocialApp.Application.Common;
using SocialApp.Application.DTOs.Groups;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Interfaces.Services;

public interface IGroupService
{
    // ── CRUD ───────────────────────────────────────────────────────────
    Task<GroupDetailDto> CreateGroupAsync(Guid userId, CreateGroupDto dto, CancellationToken ct = default);
    Task<GroupDetailDto> UpdateGroupAsync(Guid userId, Guid groupId, UpdateGroupDto dto, CancellationToken ct = default);
    Task DeleteGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task<GroupDetailDto> GetGroupAsync(Guid groupId, Guid viewerId, CancellationToken ct = default);
    Task<PagedResult<GroupSummaryDto>> SearchGroupsAsync(Guid viewerId, string? keyword, int page, int size, CancellationToken ct = default);
    Task<PagedResult<GroupSummaryDto>> GetMyGroupsAsync(Guid userId, int page, int size, CancellationToken ct = default);

    // ── Member ─────────────────────────────────────────────────────────
    Task<object> JoinGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task LeaveGroupAsync(Guid userId, Guid groupId, CancellationToken ct = default);
    Task KickMemberAsync(Guid requesterId, Guid groupId, Guid targetUserId, CancellationToken ct = default);
    Task UpdateMemberRoleAsync(Guid requesterId, Guid groupId, Guid targetUserId, GroupRole newRole, CancellationToken ct = default);
    Task<PagedResult<GroupMemberDto>> GetMembersAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default);

    // ── Join Request ───────────────────────────────────────────────────
    Task ReviewJoinRequestAsync(Guid reviewerId, Guid groupId, Guid requestId, ApproveJoinRequestDto dto, CancellationToken ct = default);
    Task<PagedResult<GroupJoinRequestDto>> GetPendingJoinRequestsAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default);
    Task CancelJoinRequestAsync(Guid userId, Guid groupId, CancellationToken ct = default);

    // ── Group Post ─────────────────────────────────────────────────────
    Task<PostResponseDto> CreateGroupPostAsync(Guid userId, Guid groupId, CreateGroupPostDto dto, CancellationToken ct = default);
    Task<PagedResult<PostResponseDto>> GetGroupFeedAsync(Guid viewerId, Guid groupId, int page, int size, Guid? cursorId, CancellationToken ct = default);
    Task<PagedResult<PostResponseDto>> GetPendingPostsAsync(Guid requesterId, Guid groupId, int page, int size, CancellationToken ct = default);
    Task ReviewGroupPostAsync(Guid reviewerId, Guid groupId, Guid postId, ReviewGroupPostDto dto, CancellationToken ct = default);
}
