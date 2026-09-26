using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class AuditLogQueryService(IAuditLogQueryRepository repository) : IAuditLogQueryService
{
    public async Task<AuditLogListResponseDto> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? actor,
        string? role,
        string? action,
        string? objectType,
        string? objectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1)
        {
            throw new AuditLogQueryValidationException("page 必須大於等於 1。");
        }

        if (pageSize is < 1 or > 100)
        {
            throw new AuditLogQueryValidationException("pageSize 必須介於 1 到 100。");
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value > toUtc.Value)
        {
            throw new AuditLogQueryValidationException("fromUtc 不可晚於 toUtc。");
        }

        var result = await repository.QueryAsync(
            fromUtc,
            toUtc,
            NormalizeOptional(actor),
            NormalizeOptional(role),
            NormalizeOptional(action),
            NormalizeOptional(objectType),
            NormalizeOptional(objectId),
            page,
            pageSize,
            cancellationToken);

        return new AuditLogListResponseDto(
            result.Items.Select(ToDto).ToList(),
            page,
            pageSize,
            result.Total);
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static AuditLogDto ToDto(AuditLog auditLog) => new(
        auditLog.Id,
        auditLog.OccurredAtUtc,
        auditLog.Actor,
        auditLog.Role,
        auditLog.Action,
        auditLog.ObjectType,
        auditLog.ObjectId,
        auditLog.BeforeJson,
        auditLog.AfterJson,
        auditLog.SourceIp);
}

public sealed class AuditLogQueryValidationException(string message) : Exception(message);
