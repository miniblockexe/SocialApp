using AutoMapper;
using SocialApp.Application.DTOs.Admin;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Admin module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
///
/// Lưu ý: MediaCount, LikeCount, CommentCount (Post) và
/// PostCount, FriendCount, MessageCount (User) được Ignore ở đây —
/// AdminService tính thủ công sau khi map vì các giá trị này
/// yêu cầu filter riêng (IsDeleted=false, status=Accepted...).
/// </summary>
public sealed class AdminMappingProfile : Profile
{
    public AdminMappingProfile()
    {
        // Post → AdminPostDto
        // Id, Content, Privacy, CreatedAt, UpdatedAt map theo convention (tên khớp).
        // IsDeleted map từ computed property BaseAuditableEntity.IsDeleted.
        // DeletedAt map theo convention.
        // Author lấy từ Post.User — tái dùng mapping User → UserBriefDto
        //   đã khai báo trong AuthMappingProfile (AutoMapper gộp mọi profile).
        // DeletedByAdmin, AdminDeleteReason: Post entity chưa có 2 field này
        //   → Ignore, AdminService set thủ công nếu cần (hoặc mở rộng entity sau).
        // MediaCount, LikeCount, CommentCount: Ignore — AdminService tính sau khi map.
        CreateMap<Post, AdminPostDto>()
            .ForMember(dest => dest.Author,
                opt => opt.MapFrom(src => src.User))
            .ForMember(dest => dest.IsDeleted,
                opt => opt.MapFrom(src => src.IsDeleted))
            .ForMember(dest => dest.DeletedAt,
                opt => opt.MapFrom(src => src.DeletedAt))
            .ForMember(dest => dest.DeletedByAdmin,
                opt => opt.Ignore())
            .ForMember(dest => dest.AdminDeleteReason,
                opt => opt.Ignore())
            .ForMember(dest => dest.MediaCount,
                opt => opt.Ignore())
            .ForMember(dest => dest.LikeCount,
                opt => opt.Ignore())
            .ForMember(dest => dest.CommentCount,
                opt => opt.Ignore());

        // User → AdminUserDto
        // Id, Username, Email, FullName, AvatarUrl, Role, IsActive,
        // IsBanned, BannedReason, CreatedAt, LastSeen map theo convention.
        // PostCount, FriendCount, MessageCount: Ignore — AdminService tính sau khi map.
        // KHÔNG map PasswordHash — field này không có trong AdminUserDto (an toàn theo thiết kế).
        CreateMap<User, AdminUserDto>()
            .ForMember(dest => dest.PostCount,
                opt => opt.Ignore())
            .ForMember(dest => dest.FriendCount,
                opt => opt.Ignore())
            .ForMember(dest => dest.MessageCount,
                opt => opt.Ignore());
    }
}