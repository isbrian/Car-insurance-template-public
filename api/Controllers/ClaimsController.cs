using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/claims")]
public sealed class ClaimsController(IClaimService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClaimDto>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] int? policyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetAllAsync(status, policyId, cancellationToken));
        }
        catch (ClaimValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ClaimDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ClaimDto>> Create(
        ClaimCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return Created($"/api/claims/{result.Id}", result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimNotFoundException ex) { return NotFoundPolicy(ex.Message); }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "理賠資料衝突", detail: "理賠案件編號與目前資料衝突，請重試。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ClaimDto>> Update(
        int id,
        ClaimUpdateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await service.DeleteAsync(id, cancellationToken)
                ? NoContent()
                : NotFoundClaim($"找不到 Id={id} 的理賠案件。");
        }
        catch (ClaimConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:int}/liability")]
    [Authorize(Policy = AppPolicies.ClaimSettle)]
    public async Task<ActionResult<ClaimDto>> CompleteLiability(int id, ClaimLiabilityDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CompleteLiabilityAsync(id, request, GetAuditActor(), cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpPost("{id:int}/approve")]
    [Authorize(Policy = AppPolicies.ClaimSettle)]
    public async Task<ActionResult<ClaimDto>> ApproveAmount(int id, ClaimApproveDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ApproveAmountAsync(id, request, GetAuditActor(), cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpPost("{id:int}/pay")]
    [Authorize(Policy = AppPolicies.ClaimSettle)]
    public async Task<ActionResult<ClaimDto>> CompletePayment(int id, ClaimPayDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CompletePaymentAsync(id, request, GetAuditActor(), cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    [HttpPost("{id:int}/deny")]
    [Authorize(Policy = AppPolicies.ClaimSettle)]
    public async Task<ActionResult<ClaimDto>> Deny(int id, ClaimDenyDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.DenyAsync(id, request, GetAuditActor(), cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    private AuditActorContext GetAuditActor() => new(
        User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
        User.FindFirst("role")?.Value ?? string.Empty,
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    [HttpPost("{id:int}/transition")]
    public async Task<ActionResult<ClaimDto>> Transition(
        int id,
        ClaimTransitionDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.TransitionAsync(id, request, cancellationToken);
            return result is null ? NotFoundClaim($"找不到 Id={id} 的理賠案件。") : Ok(result);
        }
        catch (ClaimValidationException ex) { return Invalid(ex.Message); }
        catch (ClaimConflictException ex) { return ConflictProblem(ex.Message); }
    }

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "理賠資料無效", detail: detail);

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "理賠資料衝突", detail: detail);

    private ActionResult NotFoundClaim(string detail) =>
        Problem(statusCode: 404, title: "理賠案件不存在", detail: detail);

    private ActionResult NotFoundPolicy(string detail) =>
        Problem(statusCode: 404, title: "保單不存在", detail: detail);
}
