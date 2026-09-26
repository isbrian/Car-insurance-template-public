using System.Text.Json;
using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class UserManagementService(
    IUserManagementRepository repository,
    IAuditLogService auditLogService) : IUserManagementService
{
    public async Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<UserAccountDto?> GetUserAsync(int id, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(id, cancellationToken);
        return user is null ? null : Map(user);
    }

    public async Task<UserAccountDto?> UpdateActiveAsync(
        int id,
        UserActiveUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null) return null;
        if (user.IsActive == request.IsActive) return Map(user);

        var actor = RequireAuditActor(auditActor);
        var before = user.IsActive;
        user.IsActive = request.IsActive;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "user.active.changed",
                "UserAccount",
                user.Id.ToString(),
                JsonSerializer.Serialize(new { isActive = before }),
                JsonSerializer.Serialize(new { isActive = user.IsActive }),
                actor.SourceIp),
            cancellationToken);

        return Map(user);
    }

    public async Task<UserAccountDto?> UpdateRoleAsync(
        int id,
        UserRoleUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var role = request.Role.Trim();
        if (!AppRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new UserManagementValidationException("role 必須為既有固定角色。");
        }

        var user = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (user is null) return null;
        if (string.Equals(user.Role, role, StringComparison.Ordinal)) return Map(user);

        var actor = RequireAuditActor(auditActor);
        var before = user.Role;
        user.Role = role;
        user.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "user.role.changed",
                "UserAccount",
                user.Id.ToString(),
                JsonSerializer.Serialize(new { role = before }),
                JsonSerializer.Serialize(new { role = user.Role }),
                actor.SourceIp),
            cancellationToken);

        return Map(user);
    }

    private static AuditActorContext RequireAuditActor(AuditActorContext auditActor)
    {
        var actor = auditActor.Actor.Trim();
        var role = auditActor.Role.Trim();
        var sourceIp = auditActor.SourceIp.Trim();
        if (actor.Length == 0 || role.Length == 0 || sourceIp.Length == 0)
        {
            throw new InvalidOperationException("缺少可信的稽核操作人、角色或來源 IP。");
        }

        return new AuditActorContext(actor, role, sourceIp);
    }

    private static UserAccountDto Map(UserAccount user) =>
        new(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.Phone,
            user.Role,
            user.IsActive,
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
}

public sealed class UserManagementValidationException(string message) : Exception(message);
