using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public sealed record RenewalPolicySource(
    Policy Policy,
    PolicyLifecycle? Lifecycle,
    int? ExistingRenewalQuoteId);

public interface IRenewalRepository
{
    Task<IReadOnlyList<RenewalPolicySource>> GetEligibleAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken);
    Task<RenewalPolicySource?> GetSourceByPolicyIdAsync(int policyId, CancellationToken cancellationToken);
    Task<int?> GetCustomerClaimRecordLevelAsync(int customerId, CancellationToken cancellationToken);
    Task<int> CountPaidClaimsAsync(int customerId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    Task<string?> GetLatestQuoteNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddQuoteAsync(Quote quote, CancellationToken cancellationToken);
    Task<Quote?> GetQuoteByIdAsync(int id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
