using AutoMapper;
using SocialApp.Application.DTOs.Users;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho User module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
/// </summary>
public sealed class UserMappingProfile : Profile
{
    public UserMappingProfile()
    {
        // User → UserProfileDto
        // Id, Username, FullName, Bio, AvatarUrl, CoverPhotoUrl, CreatedAt map theo convention
        // (tên property khớp nhau giữa User và UserProfileDto).
        // FriendCount, PostCount, FriendshipStatus không tồn tại trên User entity — được
        // UserService set thủ công sau khi map, nên ignore ở đây.
        CreateMap<User, UserProfileDto>()
            .ForMember(dest => dest.FriendCount, opt => opt.Ignore())
            .ForMember(dest => dest.PostCount, opt => opt.Ignore())
            .ForMember(dest => dest.FriendshipStatus, opt => opt.Ignore());

        // User → UserSearchResultDto
        // Id, Username, FullName, AvatarUrl map theo convention.
        // MutualFriendsCount, FriendshipStatus được UserService tính thủ công.
        CreateMap<User, UserSearchResultDto>()
            .ForMember(dest => dest.MutualFriendsCount, opt => opt.Ignore())
            .ForMember(dest => dest.FriendshipStatus, opt => opt.Ignore());
    }
}