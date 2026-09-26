using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IUnderwritingService
{
    Task<IReadOnlyList<UnderwritingRuleDto>> GetRulesAsync(CancellationToken cancellationToken);
    Task<UnderwritingRuleDto?> GetRuleAsync(int id, CancellationToken cancellationToken);
    Task<UnderwritingRuleDto> CreateRuleAsync(
        UnderwritingRuleCreateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<UnderwritingRuleDto?> UpdateRuleAsync(
        int id,
        UnderwritingRuleUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<bool> DeleteRuleAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<UnderwritingEvaluationDto?> EvaluateAsync(int applicationId, CancellationToken cancellationToken);
    Task<UnderwritingEvaluationDto?> GetResultsAsync(int applicationId, CancellationToken cancellationToken);
}
