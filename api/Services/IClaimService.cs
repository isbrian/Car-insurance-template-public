using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IClaimService
{
    Task<IReadOnlyList<ClaimDto>> GetAllAsync(string? status, int? policyId, CancellationToken cancellationToken);
    Task<ClaimDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<ClaimDto> CreateAsync(ClaimCreateDto request, CancellationToken cancellationToken);
    Task<ClaimDto?> UpdateAsync(int id, ClaimUpdateDto request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<ClaimDto?> TransitionAsync(int id, ClaimTransitionDto request, CancellationToken cancellationToken);
    Task<ClaimDto?> CompleteLiabilityAsync(int id, ClaimLiabilityDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<ClaimDto?> ApproveAmountAsync(int id, ClaimApproveDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<ClaimDto?> CompletePaymentAsync(int id, ClaimPayDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<ClaimDto?> DenyAsync(int id, ClaimDenyDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
}
