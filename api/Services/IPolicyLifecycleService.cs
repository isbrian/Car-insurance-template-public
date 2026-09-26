using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IPolicyLifecycleService
{
    Task<PolicyLifecycleDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> EnsureForQuoteAsync(int quoteId, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto> AttachApplicationAndSubmitAsync(int quoteId, int applicationId, CancellationToken cancellationToken);
    Task PrepareApplicationSubmissionAsync(int quoteId, int applicationId, CancellationToken cancellationToken);
    Task PrepareUnderwritingStatusAsync(int applicationId, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
    Task PreparePaymentStatusAsync(int applicationId, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
    Task PrepareEndorsementStatusAsync(int applicationId, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> TransitionAsync(int id, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> TransitionByApplicationAsync(int applicationId, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
    Task<PolicyLifecycleDto?> SyncUnderwritingStatusAsync(int applicationId, string targetStatus, string transition, string? reason, CancellationToken cancellationToken);
}
