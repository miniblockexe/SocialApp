using AutoMapper;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Notifications;
using SocialApp.Domain.Entities;
using SocialApp.Domain.Enums;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Notification module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
/// </summary>
public sealed class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        // Notification → NotificationDto
        // Id, Type, Content, IsRead, CreatedAt, EntityId map theo convention.
        // Actor map từ navigation property Notification.Actor (User).
        // EntityType không có trên entity — service resolve và set thủ công,
        // nên ignore ở đây để tránh AutoMapper cố map sai.
        CreateMap<Notification, NotificationDto>()
            .ForMember(dest => dest.Actor,
                opt => opt.MapFrom(src => src.Actor))
            .ForMember(dest => dest.EntityType,
                opt => opt.MapFrom(src => ResolveEntityType(src.Type)));
    }

    /// <summary>
    /// Resolve EntityType string từ NotificationType để client điều hướng đúng route.
    /// "post" | "friend_request" | "message" | "system"
    /// </summary>
    private static string ResolveEntityType(NotificationType type) => type switch
    {
        NotificationType.Like or NotificationType.Comment => "post",
        NotificationType.FriendRequest or NotificationType.FriendAccepted => "friend_request",
        NotificationType.Message => "message",
        _ => "system"
    };
}