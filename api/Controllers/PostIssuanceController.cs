using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/policies/{policyId:int}/post-issuance")]
public sealed class PostIssuanceController(IPostIssuanceService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PolicyPostIssuanceTimelineDto>> Process(
        int policyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.ProcessAsync(policyId, cancellationToken));
        }
        catch (PostIssuanceNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單不存在", detail: ex.Message);
        }
        catch (PostIssuanceConflictException ex)
        {
            return Problem(statusCode: 409, title: "出單後處理失敗", detail: ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<PolicyPostIssuanceTimelineDto>> GetTimeline(
        int policyId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetTimelineAsync(policyId, cancellationToken);
        return result is null
            ? Problem(statusCode: 404, title: "保單不存在", detail: $"找不到 Id={policyId} 的保單。")
            : Ok(result);
    }
}
