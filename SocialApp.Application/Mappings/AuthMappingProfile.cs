using AutoMapper;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

/// <summary>
/// AutoMapper profile cho Auth module.
/// Scan tự động bởi ServiceCollectionExtensions.AddApplicationServices()
/// qua Assembly của AssemblyMarker.
/// </summary>
public sealed class AuthMappingProfile : Profile
{
    public AuthMappingProfile()
    {
        // User → UserBriefDto
        // AutoMapper map by convention: tên property khớp nhau, kiểu tương thích
        // Id (Guid), Username (string), FullName (string), AvatarUrl (string?), Role (UserRole)
        // — tất cả đều có trong cả User và UserBriefDto nên không cần cấu hình thêm.
        CreateMap<User, UserBriefDto>();
    }
}