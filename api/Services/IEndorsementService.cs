using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IEndorsementService
{
    Task<IReadOnlyList<EndorsementDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<EndorsementDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<EndorsementDto>> GetByPolicyIdAsync(int policyId, CancellationToken cancellationToken);
    Task<EndorsementDto> CreateAsync(EndorsementCreateDto request, CancellationToken cancellationToken);
    Task<EndorsementDto?> UpdateAsync(int id, EndorsementUpdateDto request, CancellationToken cancellationToken);
    Task<EndorsementDto?> CalculateAdjustmentAsync(int id, PremiumCalculationRequestDto request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<EndorsementDto?> SubmitAsync(int id, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<EndorsementDto?> ReviewAsync(int id, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<EndorsementDto?> ApproveAsync(int id, EndorsementDecisionDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<EndorsementDto?> RejectAsync(int id, EndorsementDecisionDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
}

public sealed class EndorsementValidationException(string message) : Exception(message);
public sealed class EndorsementNotFoundException(string message) : Exception(message);
public sealed class EndorsementConflictException(string message) : Exception(message);
