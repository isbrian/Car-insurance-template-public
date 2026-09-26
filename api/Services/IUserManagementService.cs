using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IUserManagementService
{
    Task<IReadOnlyList<UserAccountDto>> GetUsersAsync(CancellationToken cancellationToken);
    Task<UserAccountDto?> GetUserAsync(int id, CancellationToken cancellationToken);
    Task<UserAccountDto?> UpdateActiveAsync(
        int id,
        UserActiveUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<UserAccountDto?> UpdateRoleAsync(
        int id,
        UserRoleUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
}
