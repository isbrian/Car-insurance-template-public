using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public sealed class NotificationsController(INotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListResponseDto>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] string? eventType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.QueryAsync(
                keyword,
                channel,
                status,
                eventType,
                page,
                pageSize,
                cancellationToken));
        }
        catch (NotificationValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NotificationDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        return result is null ? NotFoundNotification($"找不到 Id={id} 的通知。") : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<NotificationDto>> Create(
        NotificationCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (NotificationValidationException ex)
        {
            return Invalid(ex.Message);
        }
    }

    [HttpPost("{id:int}/retry")]
    public async Task<ActionResult<NotificationDto>> Retry(
        int id,
        NotificationRetryDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.RetryAsync(id, request, GetAuditActor(), cancellationToken));
        }
        catch (NotificationNotFoundException ex)
        {
            return NotFoundNotification(ex.Message);
        }
        catch (NotificationValidationException ex)
        {
            return Invalid(ex.Message);
        }
        catch (NotificationConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    [HttpPost("{id:int}/read")]
    public async Task<ActionResult<NotificationDto>> MarkRead(int id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.MarkReadAsync(id, GetAuditActor(), cancellationToken));
        }
        catch (NotificationNotFoundException ex)
        {
            return NotFoundNotification(ex.Message);
        }
        catch (NotificationConflictException ex)
        {
            return ConflictProblem(ex.Message);
        }
    }

    private AuditActorContext GetAuditActor()
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        return new AuditActorContext(
            authenticated
                ? User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty
                : "anonymous",
            authenticated
                ? User.FindFirst("role")?.Value ?? string.Empty
                : "anonymous",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "通知資料無效", detail: detail);

    private ActionResult NotFoundNotification(string detail) =>
        Problem(statusCode: 404, title: "通知不存在", detail: detail);

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "通知狀態衝突", detail: detail);
}
