using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Policy = AppPolicies.AuditRead)]
public sealed class AuditLogsController(IAuditLogQueryService service) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(AuditLogListResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuditLogListResponseDto>> Query(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actor,
        [FromQuery] string? role,
        [FromQuery] string? action,
        [FromQuery] string? objectType,
        [FromQuery] string? objectId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.QueryAsync(
                fromUtc,
                toUtc,
                actor,
                role,
                action,
                objectType,
                objectId,
                page,
                pageSize,
                cancellationToken));
        }
        catch (AuditLogQueryValidationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "稽核查詢參數無效",
                detail: ex.Message);
        }
    }
}
