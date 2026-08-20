using AutoMapper;
using SocialApp.Application.DTOs.Posts;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Post module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
/// </summary>
public sealed class PostMappingProfile : Profile
{
    public PostMappingProfile()
    {
        // Post → PostResponseDto
        // Id, Content, Privacy, CreatedAt, UpdatedAt map theo convention.
        // Author lấy từ Post.User (dùng lại mapping User → UserBriefDto đã cấu hình ở AuthMappingProfile
        // — AutoMapper gộp mọi profile trong assembly vào 1 config nên tái dùng được).
        // MediaFiles lấy từ Post.PostMediaFiles (tên khác nhau nên cần MapFrom tường minh).
        // LikeCount, CommentCount, IsLikedByMe, IsOwner, ShareCount, IsSharedByMe, OriginalPost
        // không tồn tại trên Post entity — PostService tính thủ công sau khi map, nên ignore ở đây.
        // GroupId map trực tiếp theo convention (Post.GroupId → PostResponseDto.GroupId).
        // GroupName không tồn tại trên Post entity (lấy từ Post.Group?.Name) — ignore, service tự set.
        CreateMap<Post, PostResponseDto>()
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.User))
            .ForMember(dest => dest.MediaFiles, opt => opt.MapFrom(src => src.PostMediaFiles))
            .ForMember(dest => dest.LikeCount, opt => opt.Ignore())
            .ForMember(dest => dest.CommentCount, opt => opt.Ignore())
            .ForMember(dest => dest.IsLikedByMe, opt => opt.Ignore())
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore())
            .ForMember(dest => dest.ShareCount, opt => opt.Ignore())
            .ForMember(dest => dest.IsSharedByMe, opt => opt.Ignore())
            .ForMember(dest => dest.OriginalPost, opt => opt.Ignore())
            .ForMember(dest => dest.GroupName, opt => opt.Ignore()); // service tự set từ Post.Group?.Name

        // PostMediaFile → PostMediaDto
        // Id, MediaUrl, MediaType, StorageProvider, FileSize khớp tên hoàn toàn — không cần ForMember.
        CreateMap<PostMediaFile, PostMediaDto>();

        // Comment → CommentResponseDto
        // Id, Content, CreatedAt, UpdatedAt, ParentCommentId map theo convention.
        // Author lấy từ Comment.User. RepliesCount, IsOwner ignore — PostService tính thủ công.
        CreateMap<Comment, CommentResponseDto>()
            .ForMember(dest => dest.Author, opt => opt.MapFrom(src => src.User))
            .ForMember(dest => dest.RepliesCount, opt => opt.Ignore())
            .ForMember(dest => dest.IsOwner, opt => opt.Ignore());
    }
}