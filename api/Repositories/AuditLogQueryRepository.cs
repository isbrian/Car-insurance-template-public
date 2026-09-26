using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class AuditLogQueryRepository(CarInsuranceDbContext dbContext) : IAuditLogQueryRepository
{
    public async Task<(IReadOnlyList<AuditLog> Items, int Total)> QueryAsync(
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
        var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (actor is not null)
        {
            query = query.Where(x => x.Actor == actor);
        }

        if (role is not null)
        {
            query = query.Where(x => x.Role == role);
        }

        if (action is not null)
        {
            query = query.Where(x => x.Action == action);
        }

        if (objectType is not null)
        {
            query = query.Where(x => x.ObjectType == objectType);
        }

        if (objectId is not null)
        {
            query = query.Where(x => x.ObjectId == objectId);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }
}
