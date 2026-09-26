using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/applications/{applicationId:int}")]
public sealed class UnderwritingController(IUnderwritingService service) : ControllerBase
{
    [HttpPost("underwriting/evaluate")]
    public async Task<ActionResult<UnderwritingEvaluationDto>> Evaluate(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var evaluation = await service.EvaluateAsync(applicationId, cancellationToken);
        return evaluation is null
            ? Problem(statusCode: 404, title: "投保申請不存在", detail: $"找不到 Id={applicationId} 的投保申請。")
            : Ok(evaluation);
    }

    [HttpGet("underwriting-results")]
    public async Task<ActionResult<UnderwritingEvaluationDto>> GetResults(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var evaluation = await service.GetResultsAsync(applicationId, cancellationToken);
        return evaluation is null
            ? Problem(statusCode: 404, title: "投保申請不存在", detail: $"找不到 Id={applicationId} 的投保申請。")
            : Ok(evaluation);
    }
}
