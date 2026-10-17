using AutoMapper;
using Library.Application.DTOs.MembershipPlans;
using Library.Domain.Entities;

namespace Library.Application.Mapping;

public sealed class MembershipPlanProfile : Profile
{
    public MembershipPlanProfile()
    {
        CreateMap<MembershipPlan, MembershipPlanResponse>();
    }
}
