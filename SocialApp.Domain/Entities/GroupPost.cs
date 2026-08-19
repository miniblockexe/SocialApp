using SocialApp.Domain.Enums;

namespace SocialApp.Domain.Entities;

public class GroupPost
{
    public Guid PostId { get; set; }
    public Guid GroupId { get; set; }
    public GroupPostStatus Status { get; set; } = GroupPostStatus.Approved;
    public Guid? ReviewedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Post Post { get; set; } = null!;
    public Group Group { get; set; } = null!;
    public User? ReviewedBy { get; set; }
}
