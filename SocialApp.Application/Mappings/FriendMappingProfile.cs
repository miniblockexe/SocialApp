using AutoMapper;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Friends;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Friend module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
/// </summary>
public sealed class FriendMappingProfile : Profile
{
    public FriendMappingProfile()
    {
        // FriendRequest → FriendResponseDto
        // RequestId map từ Id, Sender/Receiver map từ navigation properties.
        // FriendSince và MutualFriendsCount KHÔNG có trên FriendRequest
        // → service set thủ công, ignore ở đây.
        CreateMap<FriendRequest, FriendResponseDto>()
            .ForMember(dest => dest.RequestId,
                opt => opt.MapFrom(src => src.Id))
            .ForMember(dest => dest.Sender,
                opt => opt.MapFrom(src => src.Sender))
            .ForMember(dest => dest.Receiver,
                opt => opt.MapFrom(src => src.Receiver))
            .ForMember(dest => dest.Status,
                opt => opt.MapFrom(src => src.Status))
            .ForMember(dest => dest.CreatedAt,
                opt => opt.MapFrom(src => src.CreatedAt))
            .ForMember(dest => dest.UpdatedAt,
                opt => opt.MapFrom(src => src.UpdatedAt));

        // User → FriendListItemDto
        // FriendSince và MutualFriendsCount không có trên User entity
        // → service tính và set thủ công sau khi map (hoặc khởi tạo trực tiếp).
        // Mapping này hỗ trợ trường hợp service dùng _mapper.Map<FriendListItemDto>(user)
        // rồi set các field thủ công — AutoMapper sẽ map User property từ nguồn.
        CreateMap<User, FriendListItemDto>()
            .ForMember(dest => dest.User,
                opt => opt.MapFrom(src => src))
            .ForMember(dest => dest.FriendSince,
                opt => opt.Ignore())
            .ForMember(dest => dest.MutualFriendsCount,
                opt => opt.Ignore());
    }
}