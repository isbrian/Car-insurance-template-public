namespace CarInsurance.Api.Repositories;

public interface IDashboardSummaryRepository
{
    Task<int> CountCustomersAsync(CancellationToken cancellationToken);
    Task<int> CountActivePoliciesAsync(CancellationToken cancellationToken);
    Task<int> CountPendingUnderwritingAsync(CancellationToken cancellationToken);
    Task<int> CountPendingClaimsAsync(CancellationToken cancellationToken);
}
