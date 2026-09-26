using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class AuditLogService(IAuditLogRepository repository) : IAuditLogService
{
    public async Task<AuditLog> WriteAsync(
        AuditLogWriteRequest request,
        CancellationToken cancellationToken)
    {
        var actor = RequireValue(request.Actor, nameof(request.Actor));
        var role = RequireValue(request.Role, nameof(request.Role));
        var action = RequireValue(request.Action, nameof(request.Action));
        var objectType = RequireValue(request.ObjectType, nameof(request.ObjectType));
        var objectId = RequireValue(request.ObjectId, nameof(request.ObjectId));
        var sourceIp = RequireValue(request.SourceIp, nameof(request.SourceIp));

        var auditLog = new AuditLog
        {
            OccurredAtUtc = DateTime.UtcNow,
            Actor = actor,
            Role = role,
            Action = action,
            ObjectType = objectType,
            ObjectId = objectId,
            BeforeJson = request.BeforeJson,
            AfterJson = request.AfterJson,
            SourceIp = sourceIp
        };

        await repository.AddAsync(auditLog, cancellationToken);
        return auditLog;
    }

    private static string RequireValue(string value, string fieldName)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            throw new AuditLogValidationException($"{fieldName} 不可為空白。");
        }

        return normalized;
    }
}

public sealed class AuditLogValidationException(string message) : Exception(message);
