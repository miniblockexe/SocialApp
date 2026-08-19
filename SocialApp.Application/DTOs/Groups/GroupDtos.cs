using Microsoft.AspNetCore.Http;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Groups;

// ── Tạo / Cập nhật nhóm ──────────────────────────────────────────────

public class CreateGroupDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public GroupPrivacy Privacy { get; set; } = GroupPrivacy.Public;
    public bool RequireApproval { get; set; } = false;
    public bool RequirePostApproval { get; set; } = false;
    public IFormFile? Avatar { get; set; }
}

public class UpdateGroupDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public GroupPrivacy? Privacy { get; set; }
    public bool? RequireApproval { get; set; }
    public bool? RequirePostApproval { get; set; }
    public IFormFile? Avatar { get; set; }
    public IFormFile? Cover { get; set; }
}

// ── Response DTOs ─────────────────────────────────────────────────────

public class GroupSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? CoverUrl { get; set; }
    public GroupPrivacy Privacy { get; set; }
    public bool RequireApproval { get; set; }
    public bool RequirePostApproval { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public GroupMembershipStatus MembershipStatus { get; set; }
    public GroupRole? ViewerRole { get; set; }
}

public class GroupDetailDto : GroupSummaryDto
{
    public UserBriefDto Owner { get; set; } = null!;
    public List<GroupMemberDto> Admins { get; set; } = [];
}

public class GroupMemberDto
{
    public UserBriefDto User { get; set; } = null!;
    public GroupRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class GroupJoinRequestDto
{
    public Guid Id { get; set; }
    public UserBriefDto User { get; set; } = null!;
    public JoinRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── Enums phụ trợ ─────────────────────────────────────────────────────

public enum GroupMembershipStatus
{
    None = 0,
    Member = 1,
    PendingApproval = 2,
}

// ── Request DTOs ──────────────────────────────────────────────────────

public class ApproveJoinRequestDto
{
    public bool Approve { get; set; }
    public string? RejectReason { get; set; }
}

public class UpdateMemberRoleDto
{
    public GroupRole Role { get; set; }
}

public class CreateGroupPostDto
{
    public string? Content { get; set; }
    public SocialApp.Domain.Enums.PostPrivacy Privacy { get; set; } = SocialApp.Domain.Enums.PostPrivacy.Public;
    public List<IFormFile>? MediaFiles { get; set; }
}

public class ReviewGroupPostDto
{
    public bool Approve { get; set; }
    public string? RejectReason { get; set; }
}