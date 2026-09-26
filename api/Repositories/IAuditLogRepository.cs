using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken);
}
