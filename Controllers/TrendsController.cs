using AllenKerberAutoSupply;
using AllenKerberAutoSupply.Models;
using AllenKerberAutoSupply.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AllenKerberAutoSupply.Controllers;

[ApiController]
[Route("api/trends")]
[Authorize(Policy = AuthorizationPolicies.ActiveAccount, Roles = RoleNames.TrendsUser)]
public sealed class TrendsController(ITrendAnalysisService service) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<TrendAnalysisResponse>> Analyze(
        [FromBody] TrendAnalysisRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.AnalyzeAsync(
                request.FromDate,
                request.ToDate,
                request.AggregateBy,
                cancellationToken));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
