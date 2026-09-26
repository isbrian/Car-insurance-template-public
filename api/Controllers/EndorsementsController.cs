using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/endorsements")]
public sealed class EndorsementsController(IEndorsementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EndorsementDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<EndorsementDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFoundEndorsement($"找不到 Id={id} 的批改單。") : Ok(result);
    }

    [HttpGet("by-policy/{policyId:int}")]
    public async Task<ActionResult<IReadOnlyList<EndorsementDto>>> GetByPolicy(
        int policyId,
        CancellationToken cancellationToken) =>
        Ok(await service.GetByPolicyIdAsync(policyId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<EndorsementDto>> Create(
        EndorsementCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (EndorsementValidationException ex) { return Invalid(ex.Message); }
        catch (EndorsementNotFoundException ex) { return Problem(statusCode: 404, title: "保單不存在", detail: ex.Message); }
        catch (EndorsementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (DbUpdateException) { return ConflictProblem("批改單編號或資料關聯與目前資料衝突。"); }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<EndorsementDto>> Update(
        int id,
        EndorsementUpdateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFoundEndorsement($"找不到 Id={id} 的批改單。") : Ok(result);
        }
        catch (EndorsementValidationException ex) { return Invalid(ex.Message); }
        catch (EndorsementConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpPost("{id:int}/calculate-adjustment")]
    public async Task<ActionResult<EndorsementDto>> CalculateAdjustment(
        int id,
        PremiumCalculationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CalculateAdjustmentAsync(id, request, cancellationToken);
            return result is null ? NotFoundEndorsement($"找不到 Id={id} 的批改單。") : Ok(result);
        }
        catch (EndorsementValidationException ex) { return Invalid(ex.Message); }
        catch (EndorsementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (PremiumCalculationValidationException ex) { return Invalid(ex.Message); }
        catch (PremiumCalculationNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "費率資料不存在", detail: ex.Message);
        }
        catch (PremiumCalculationConfigurationException ex)
        {
            return Problem(statusCode: 500, title: "費率設定無效", detail: ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await service.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFoundEndorsement($"找不到 Id={id} 的批改單。");
        }
        catch (EndorsementConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = AppPolicies.EndorsementRequest)]
    public Task<ActionResult<EndorsementDto>> Submit(int id, CancellationToken cancellationToken) =>
        ExecuteTransitionAsync(id, () => service.SubmitAsync(id, GetAuditActor(), cancellationToken));

    [HttpPost("{id:int}/review")]
    [Authorize(Policy = AppPolicies.EndorsementReview)]
    public Task<ActionResult<EndorsementDto>> Review(int id, CancellationToken cancellationToken) =>
        ExecuteTransitionAsync(id, () => service.ReviewAsync(id, GetAuditActor(), cancellationToken));

    [HttpPost("{id:int}/approve")]
    [Authorize(Roles = AppRoles.Manager)]
    public Task<ActionResult<EndorsementDto>> Approve(
        int id,
        EndorsementDecisionDto request,
        CancellationToken cancellationToken) =>
        ExecuteTransitionAsync(id, () => service.ApproveAsync(id, request, GetAuditActor(), cancellationToken));

    [HttpPost("{id:int}/reject")]
    [Authorize(Roles = AppRoles.Manager)]
    public Task<ActionResult<EndorsementDto>> Reject(
        int id,
        EndorsementDecisionDto request,
        CancellationToken cancellationToken) =>
        ExecuteTransitionAsync(id, () => service.RejectAsync(id, request, GetAuditActor(), cancellationToken));

    private AuditActorContext GetAuditActor() => new(
        User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
        User.FindFirst("role")?.Value ?? string.Empty,
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private async Task<ActionResult<EndorsementDto>> ExecuteTransitionAsync(
        int id,
        Func<Task<EndorsementDto?>> action)
    {
        try
        {
            var result = await action();
            return result is null ? NotFoundEndorsement($"找不到 Id={id} 的批改單。") : Ok(result);
        }
        catch (EndorsementValidationException ex) { return Invalid(ex.Message); }
        catch (EndorsementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (PolicyLifecycleValidationException ex) { return Invalid(ex.Message); }
        catch (PolicyLifecycleConflictException ex) { return ConflictProblem(ex.Message); }
        catch (PolicyLifecycleNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單生命週期不存在", detail: ex.Message);
        }
    }

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "批改資料無效", detail: detail);

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "批改資料衝突", detail: detail);

    private ActionResult NotFoundEndorsement(string detail) =>
        Problem(statusCode: 404, title: "批改單不存在", detail: detail);
}
