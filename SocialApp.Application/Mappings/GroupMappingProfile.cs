using AutoMapper;
using SocialApp.Application.DTOs.Auth;
using SocialApp.Application.DTOs.Groups;
using SocialApp.Domain.Entities;

namespace SocialApp.Application.Mappings;

public sealed class GroupMappingProfile : Profile
{
    public GroupMappingProfile()
    {
        CreateMap<Group, GroupSummaryDto>()
            .ForMember(d => d.MemberCount, o => o.Ignore())
            .ForMember(d => d.MembershipStatus, o => o.Ignore())
            .ForMember(d => d.ViewerRole, o => o.Ignore());

        CreateMap<Group, GroupDetailDto>()
            .ForMember(d => d.Owner, o => o.MapFrom(s => s.Owner))
            .ForMember(d => d.Admins, o => o.Ignore())
            .ForMember(d => d.MemberCount, o => o.Ignore())
            .ForMember(d => d.MembershipStatus, o => o.Ignore())
            .ForMember(d => d.ViewerRole, o => o.Ignore());

        CreateMap<GroupMember, GroupMemberDto>()
            .ForMember(d => d.User, o => o.MapFrom(s => s.User));

        CreateMap<GroupJoinRequest, GroupJoinRequestDto>()
            .ForMember(d => d.User, o => o.MapFrom(s => s.User));

        CreateMap<User, UserBriefDto>();
    }
}
