using CarInsurance.Api.Entities;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IAuditLogService
{
    Task<AuditLog> WriteAsync(AuditLogWriteRequest request, CancellationToken cancellationToken);
}
