using AutoMapper;
using Library.Application.DTOs.MembershipPlans;
using Library.Application.Interfaces;

namespace Library.Application.Services;

public sealed class MembershipPlanService : IMembershipPlanService
{
    private readonly IMembershipPlanRepository _plans;
    private readonly IMapper _mapper;

    public MembershipPlanService(IMembershipPlanRepository plans, IMapper mapper)
    {
        _plans = plans;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<MembershipPlanResponse>> GetAllAsync(CancellationToken ct)
    {
        var plans = await _plans.GetAllAsync(ct);
        return _mapper.Map<List<MembershipPlanResponse>>(plans);
    }
}
