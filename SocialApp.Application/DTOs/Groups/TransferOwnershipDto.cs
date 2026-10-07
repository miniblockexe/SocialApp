namespace SocialApp.Application.DTOs.Groups;

public class TransferOwnershipDto
{
    /// <summary>Id người dùng sẽ trở thành Owner mới (phải là thành viên của nhóm).</summary>
    public Guid NewOwnerId { get; set; }
}
