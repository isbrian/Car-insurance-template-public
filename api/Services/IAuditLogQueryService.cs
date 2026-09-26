using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IAuditLogQueryService
{
    Task<AuditLogListResponseDto> QueryAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? actor,
        string? role,
        string? action,
        string? objectType,
        string? objectId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
