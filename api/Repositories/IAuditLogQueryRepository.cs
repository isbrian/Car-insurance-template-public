using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IAuditLogQueryRepository
{
    Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryAsync(
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
