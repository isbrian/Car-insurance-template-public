using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/underwriting-queue")]
public sealed class UnderwritingQueueController(IUnderwritingQueueService service) : ControllerBase
{
    [HttpPost("sync")]
    public async Task<ActionResult<UnderwritingQueueSyncResultDto>> Sync(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.SyncAsync(cancellationToken));
        }
        catch (PolicyLifecycleValidationException ex)
        {
            return Problem(statusCode: 400, title: "保單狀態資料無效", detail: ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "保單狀態轉換衝突", detail: ex.Message);
        }
    }

    [HttpGet]
    public async Task<ActionResult<UnderwritingQueueListDto>> GetQueue(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.GetQueueAsync(keyword, status, page, pageSize, cancellationToken));
        }
        catch (UnderwritingQueueValidationException ex)
        {
            return Problem(statusCode: 400, title: "核保佇列查詢條件無效", detail: ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnderwritingCaseDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetCaseAsync(id, cancellationToken);
        return result is null
            ? Problem(statusCode: 404, title: "核保案件不存在", detail: $"找不到 Id={id} 的核保案件。")
            : Ok(result);
    }

    [HttpPost("{id:int}/reevaluate")]
    public Task<ActionResult<UnderwritingCaseDetailDto>> Reevaluate(int id, CancellationToken cancellationToken) =>
        ExecuteDecision(() => service.ReevaluateAsync(id, cancellationToken), id);

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = AppPolicies.UnderwritingDecide)]
    public Task<ActionResult<UnderwritingCaseDetailDto>> Approve(
        int id,
        UnderwritingDecisionRequestDto request,
        CancellationToken cancellationToken) =>
        ExecuteDecision(() => service.ApproveAsync(id, request, GetAuditActor(), cancellationToken), id);

    [HttpPost("{id:int}/request-documents")]
    public Task<ActionResult<UnderwritingCaseDetailDto>> RequestDocuments(
        int id,
        UnderwritingDecisionRequestDto request,
        CancellationToken cancellationToken) =>
        ExecuteDecision(() => service.RequestDocumentsAsync(id, request, cancellationToken), id);

    [HttpPost("{id:int}/reject")]
    [Authorize(Policy = AppPolicies.UnderwritingDecide)]
    public Task<ActionResult<UnderwritingCaseDetailDto>> Reject(
        int id,
        UnderwritingDecisionRequestDto request,
        CancellationToken cancellationToken) =>
        ExecuteDecision(() => service.RejectAsync(id, request, GetAuditActor(), cancellationToken), id);

    private AuditActorContext GetAuditActor() => new(
        User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
        User.FindFirst("role")?.Value ?? string.Empty,
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private async Task<ActionResult<UnderwritingCaseDetailDto>> ExecuteDecision(
        Func<Task<UnderwritingCaseDetailDto?>> action,
        int id)
    {
        try
        {
            var result = await action();
            return result is null
                ? Problem(statusCode: 404, title: "核保案件不存在", detail: $"找不到 Id={id} 的核保案件。")
                : Ok(result);
        }
        catch (UnderwritingQueueValidationException ex)
        {
            return Problem(statusCode: 400, title: "核保處置資料無效", detail: ex.Message);
        }
        catch (UnderwritingQueueConflictException ex)
        {
            return Problem(statusCode: 409, title: "核保處置衝突", detail: ex.Message);
        }
        catch (PolicyLifecycleValidationException ex)
        {
            return Problem(statusCode: 400, title: "保單狀態資料無效", detail: ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "保單狀態轉換衝突", detail: ex.Message);
        }
    }
}
