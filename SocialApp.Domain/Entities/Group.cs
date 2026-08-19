using SocialApp.Domain.Common;
using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

public class Group : BaseAuditableEntity
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public string? AvatarPublicId { get; set; }
    public string? CoverUrl { get; set; }
    public string? CoverPublicId { get; set; }
    public GroupPrivacy Privacy { get; set; } = GroupPrivacy.Public;
    public bool RequireApproval { get; set; } = false;
    public bool RequirePostApproval { get; set; } = false;

    public User Owner { get; set; } = null!;
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<GroupPost> GroupPosts { get; set; } = new List<GroupPost>();
    public ICollection<GroupJoinRequest> JoinRequests { get; set; } = new List<GroupJoinRequest>();
}
