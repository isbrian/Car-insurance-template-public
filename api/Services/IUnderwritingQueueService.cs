using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IUnderwritingQueueService
{
    Task<UnderwritingQueueSyncResultDto> SyncAsync(CancellationToken cancellationToken);
    Task<UnderwritingQueueListDto> GetQueueAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<UnderwritingCaseDetailDto?> GetCaseAsync(int id, CancellationToken cancellationToken);
    Task<UnderwritingCaseDetailDto?> ReevaluateAsync(int id, CancellationToken cancellationToken);
    Task<UnderwritingCaseDetailDto?> ApproveAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<UnderwritingCaseDetailDto?> RequestDocumentsAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        CancellationToken cancellationToken);
    Task<UnderwritingCaseDetailDto?> RejectAsync(
        int id,
        UnderwritingDecisionRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
}
