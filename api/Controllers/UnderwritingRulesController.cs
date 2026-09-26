using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/underwriting-rules")]
public sealed class UnderwritingRulesController(IUnderwritingService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UnderwritingRuleDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetRulesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UnderwritingRuleDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var rule = await service.GetRuleAsync(id, cancellationToken);
        return rule is null
            ? Problem(statusCode: 404, title: "核保規則不存在", detail: $"找不到 Id={id} 的核保規則。")
            : Ok(rule);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.UnderwritingRuleManage)]
    public async Task<ActionResult<UnderwritingRuleDto>> Create(
        UnderwritingRuleCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await service.CreateRuleAsync(request, GetAuditActor(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = rule.Id }, rule);
        }
        catch (UnderwritingValidationException ex)
        {
            return Problem(statusCode: 400, title: "核保規則資料無效", detail: ex.Message);
        }
        catch (UnderwritingConflictException ex)
        {
            return Problem(statusCode: 409, title: "核保規則衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "核保規則衝突", detail: "核保規則資料與目前資料狀態衝突。");
        }
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AppPolicies.UnderwritingRuleManage)]
    public async Task<ActionResult<UnderwritingRuleDto>> Update(
        int id,
        UnderwritingRuleUpdateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rule = await service.UpdateRuleAsync(id, request, GetAuditActor(), cancellationToken);
            return rule is null
                ? Problem(statusCode: 404, title: "核保規則不存在", detail: $"找不到 Id={id} 的核保規則。")
                : Ok(rule);
        }
        catch (UnderwritingValidationException ex)
        {
            return Problem(statusCode: 400, title: "核保規則資料無效", detail: ex.Message);
        }
        catch (UnderwritingConflictException ex)
        {
            return Problem(statusCode: 409, title: "核保規則衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "核保規則衝突", detail: "核保規則資料與目前資料狀態衝突。");
        }
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AppPolicies.UnderwritingRuleManage)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await service.DeleteRuleAsync(id, GetAuditActor(), cancellationToken)
                ? NoContent()
                : Problem(statusCode: 404, title: "核保規則不存在", detail: $"找不到 Id={id} 的核保規則。");
        }
        catch (UnderwritingConflictException ex)
        {
            return Problem(statusCode: 409, title: "核保規則仍被使用", detail: ex.Message);
        }
    }

    private AuditActorContext GetAuditActor() =>
        new(
            User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
            User.FindFirst("role")?.Value ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
