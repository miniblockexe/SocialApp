using AutoMapper;
using SocialApp.Application.DTOs.Messages;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Message module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
///
/// Lưu ý: MessageService hiện tạo MessageDto thủ công qua MapToMessageDto()
/// (vì cần conditional null khi IsDeleted, và SeenBy phải được Include trước).
/// Profile này chuẩn hoá ánh xạ và tạo nền cho refactor sau.
///
/// ConversationDto.LastMessage, UnreadCount, Participants — Ignore ở AutoMapper,
/// service tính thủ công sau khi query DB.
/// </summary>
public sealed class MessageMappingProfile : Profile
{
    public MessageMappingProfile()
    {
        // Message → MessageDto
        // Content, AttachmentUrl, AttachmentType: ẩn nếu IsDeleted = true
        // SeenByUserIds: flatten từ ICollection<MessageSeen>
        // Sender: tái dùng mapping User → UserBriefDto (định nghĩa ở AuthMappingProfile)
        CreateMap<Message, MessageDto>()
            .ForMember(dest => dest.Content,
                opt => opt.MapFrom(src => src.IsDeleted ? null : src.Content))
            .ForMember(dest => dest.AttachmentUrl,
                opt => opt.MapFrom(src => src.IsDeleted ? null : src.AttachmentUrl))
            .ForMember(dest => dest.AttachmentType,
                opt => opt.MapFrom(src => src.IsDeleted ? null : src.AttachmentType))
            .ForMember(dest => dest.SeenByUserIds,
                opt => opt.MapFrom(src => src.SeenBy.Select(s => s.UserId).ToList()))
            .ForMember(dest => dest.Sender,
                opt => opt.MapFrom(src => src.Sender));

        // Conversation → ConversationDto
        // Id, IsGroup, GroupName, GroupAvatarUrl, LastMessageAt — convention (tên khớp).
        // LastMessage, UnreadCount, Participants — service tính thủ công, Ignore tại đây.
        CreateMap<Conversation, ConversationDto>()
            .ForMember(dest => dest.LastMessage,
                opt => opt.Ignore())
            .ForMember(dest => dest.UnreadCount,
                opt => opt.Ignore())
            .ForMember(dest => dest.Participants,
                opt => opt.Ignore());
    }
}