using Library.Application.DTOs.MembershipPlans;
using Library.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Library.API.Controllers;

[Route("api/membership-plans")]
public sealed class MembershipPlansController : ApiControllerBase
{
    private readonly IMembershipPlanService _plans;

    public MembershipPlansController(IMembershipPlanService plans)
    {
        _plans = plans;
    }

    // Open: the registration screen needs the plans before the user has a token.
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<MembershipPlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MembershipPlanResponse>>> GetAll(CancellationToken ct)
    {
        return Ok(await _plans.GetAllAsync(ct));
    }
}
