using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(IPaymentService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetByIdAsync(id, cancellationToken);
        return result is null ? NotFoundPayment($"找不到 Id={id} 的付款資料。") : Ok(result);
    }

    [HttpGet("by-application/{applicationId:int}")]
    public async Task<ActionResult<PaymentDto>> GetByApplication(int applicationId, CancellationToken cancellationToken)
    {
        var result = await service.GetByApplicationIdAsync(applicationId, cancellationToken);
        return result is null ? NotFoundPayment($"找不到 ApplicationId={applicationId} 的付款資料。") : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> Create(PaymentCreateDto request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (PaymentValidationException ex)
        {
            return Invalid(ex.Message);
        }
        catch (PaymentNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "投保申請不存在", detail: ex.Message);
        }
        catch (PaymentConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("付款編號或投保申請關聯與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<PaymentDto>> Update(
        int id,
        PaymentUpdateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.UpdateAsync(id, request, cancellationToken);
            return result is null ? NotFoundPayment($"找不到 Id={id} 的付款資料。") : Ok(result);
        }
        catch (PaymentValidationException ex)
        {
            return Invalid(ex.Message);
        }
        catch (PaymentConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:int}/confirm")]
    [Authorize(Policy = AppPolicies.PolicyIssue)]
    public async Task<ActionResult<PaymentDto>> Confirm(
        int id,
        PaymentConfirmDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ConfirmAsync(id, request, GetAuditActor(), cancellationToken);
            return result is null ? NotFoundPayment($"找不到 Id={id} 的付款資料。") : Ok(result);
        }
        catch (PaymentValidationException ex)
        {
            return Invalid(ex.Message);
        }
        catch (PaymentConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (PolicyLifecycleNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單生命週期不存在", detail: ex.Message);
        }
    }

    [HttpPost("{id:int}/mark-overdue")]
    public async Task<ActionResult<PaymentDto>> MarkOverdue(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.MarkOverdueAsync(id, cancellationToken);
            return result is null ? NotFoundPayment($"找不到 Id={id} 的付款資料。") : Ok(result);
        }
        catch (PaymentConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (PolicyLifecycleNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單生命週期不存在", detail: ex.Message);
        }
    }

    [HttpPost("sync-overdue")]
    public async Task<ActionResult<PaymentSyncOverdueResultDto>> SyncOverdue(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.SyncOverdueAsync(cancellationToken));
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (PolicyLifecycleNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單生命週期不存在", detail: ex.Message);
        }
    }

    private AuditActorContext GetAuditActor() => new(
        User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
        User.FindFirst("role")?.Value ?? string.Empty,
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "付款資料無效", detail: detail);

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "付款資料衝突", detail: detail);

    private ActionResult NotFoundPayment(string detail) =>
        Problem(statusCode: 404, title: "付款資料不存在", detail: detail);
}
