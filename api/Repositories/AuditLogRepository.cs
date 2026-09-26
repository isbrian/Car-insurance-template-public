using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public sealed class AuditLogRepository(CarInsuranceDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken)
    {
        await dbContext.AuditLogs.AddAsync(auditLog, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
