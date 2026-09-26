using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/policy-lifecycles")]
public sealed class PolicyLifecyclesController(IPolicyLifecycleService service) : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PolicyLifecycleDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFoundLifecycle($"找不到 Id={id} 的保單生命週期。") : Ok(result);
    }

    [HttpGet("by-quote/{quoteId:int}")]
    public async Task<ActionResult<PolicyLifecycleDto>> GetByQuote(int quoteId, CancellationToken cancellationToken)
    {
        var result = await service.GetByQuoteIdAsync(quoteId, cancellationToken);
        return result is null ? NotFoundLifecycle($"找不到 QuoteId={quoteId} 的報價。") : Ok(result);
    }

    [HttpGet("by-application/{applicationId:int}")]
    public async Task<ActionResult<PolicyLifecycleDto>> GetByApplication(int applicationId, CancellationToken cancellationToken)
    {
        var result = await service.GetByApplicationIdAsync(applicationId, cancellationToken);
        return result is null ? NotFoundLifecycle($"找不到 ApplicationId={applicationId} 的保單生命週期。") : Ok(result);
    }

    [HttpPost("{id:int}/transition")]
    public async Task<ActionResult<PolicyLifecycleDto>> Transition(
        int id,
        PolicyLifecycleTransitionDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.TransitionAsync(
                id,
                request.TargetStatus,
                request.Transition,
                request.Reason,
                cancellationToken);
            return result is null ? NotFoundLifecycle($"找不到 Id={id} 的保單生命週期。") : Ok(result);
        }
        catch (PolicyLifecycleValidationException ex)
        {
            return Problem(statusCode: 400, title: "保單狀態轉換資料無效", detail: ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "保單狀態轉換衝突", detail: ex.Message);
        }
    }

    private ActionResult NotFoundLifecycle(string detail) =>
        Problem(statusCode: 404, title: "保單生命週期不存在", detail: detail);
}
