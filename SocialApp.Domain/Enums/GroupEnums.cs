namespace SocialApp.Domain.Enums;

public enum GroupPrivacy
{
    Public = 0,
    Private = 1,
}

public enum GroupRole
{
    Member = 0,
    Admin = 1,
    Owner = 2,
}

public enum JoinRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public enum GroupPostStatus
{
    Approved = 0,
    Pending = 1,
    Rejected = 2,
}
