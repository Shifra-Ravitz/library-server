using AutoMapper;
using Library.Application.DTOs.Users;
using Library.Domain.Entities;

namespace Library.Application.Mapping;

public sealed class UserProfile : Profile
{
    public UserProfile()
    {
        // MembershipPlanName is flattened from MembershipPlan.Name (null for librarians).
        CreateMap<User, UserResponse>()
            .ForMember(d => d.Role, o => o.MapFrom(s => s.Role.ToString()));
    }
}
