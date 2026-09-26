using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AppPolicies.SystemAdministrator)]
public sealed class UsersController(IUserManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserAccountDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetUsersAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<UserAccountDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var user = await service.GetUserAsync(id, cancellationToken);
        return user is null
            ? Problem(statusCode: 404, title: "使用者不存在", detail: $"找不到 Id={id} 的使用者。")
            : Ok(user);
    }

    [HttpPut("{id:int}/active")]
    public async Task<ActionResult<UserAccountDto>> UpdateActive(
        int id,
        UserActiveUpdateDto request,
        CancellationToken cancellationToken)
    {
        var user = await service.UpdateActiveAsync(id, request, GetAuditActor(), cancellationToken);
        return user is null
            ? Problem(statusCode: 404, title: "使用者不存在", detail: $"找不到 Id={id} 的使用者。")
            : Ok(user);
    }

    [HttpPut("{id:int}/role")]
    public async Task<ActionResult<UserAccountDto>> UpdateRole(
        int id,
        UserRoleUpdateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await service.UpdateRoleAsync(id, request, GetAuditActor(), cancellationToken);
            return user is null
                ? Problem(statusCode: 404, title: "使用者不存在", detail: $"找不到 Id={id} 的使用者。")
                : Ok(user);
        }
        catch (UserManagementValidationException ex)
        {
            return Problem(statusCode: 400, title: "使用者角色無效", detail: ex.Message);
        }
    }

    private AuditActorContext GetAuditActor() =>
        new(
            User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
            User.FindFirst("role")?.Value ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
