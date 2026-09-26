using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/policies")]
public sealed class PoliciesController(IPolicyService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PolicyListResponse>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "分頁參數無效",
                detail: "page 必須大於 0，pageSize 必須介於 1 至 100。");
        }

        return Ok(await service.SearchAsync(keyword, status, page, pageSize, cancellationToken));
    }

    [HttpPost("issue/{applicationId:int}")]
    [Authorize(Policy = AppPolicies.PolicyIssue)]
    public async Task<ActionResult<PolicyDto>> Issue(int applicationId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.IssueAsync(applicationId, GetAuditActor(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (PolicyValidationException ex)
        {
            return Problem(statusCode: 400, title: "出單資料無效", detail: ex.Message);
        }
        catch (PolicyNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "投保申請不存在", detail: ex.Message);
        }
        catch (PolicyConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("保單編號、投保申請或付款關聯與目前資料衝突。");
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PolicyDetailsDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetDetailsAsync(id, cancellationToken);
        return result is null
            ? Problem(statusCode: 404, title: "保單不存在", detail: $"找不到 Id={id} 的保單。")
            : Ok(result);
    }

    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<PolicyTimelineDto>> GetTimeline(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetTimelineAsync(id, cancellationToken);
        return result is null
            ? Problem(statusCode: 404, title: "保單不存在", detail: $"找不到 Id={id} 的保單。")
            : Ok(result);
    }

    [HttpGet("by-application/{applicationId:int}")]
    public async Task<ActionResult<PolicyDto>> GetByApplication(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var result = await service.GetByApplicationIdAsync(applicationId, cancellationToken);
        return result is null
            ? Problem(statusCode: 404, title: "保單不存在", detail: $"找不到 ApplicationId={applicationId} 的保單。")
            : Ok(result);
    }

    [HttpGet("{id:int}/document")]
    public async Task<IActionResult> GetDocument(int id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.GetDocumentAsync(id, cancellationToken);
            return result is null
                ? Problem(statusCode: 404, title: "保單不存在", detail: $"找不到 Id={id} 的保單。")
                : File(result.Content, result.ContentType, result.FileName);
        }
        catch (PolicyDocumentNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單文件不存在", detail: ex.Message);
        }
    }

    private AuditActorContext GetAuditActor() => new(
        User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
        User.FindFirst("role")?.Value ?? string.Empty,
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "出單資料衝突", detail: detail);
}
