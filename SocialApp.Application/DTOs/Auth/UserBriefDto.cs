using SocialApp.Domain.Enums;

namespace SocialApp.Application.DTOs.Auth;

public sealed class UserBriefDto
{
    public Guid Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public UserRole Role { get; init; }
}