using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IPolicyLifecycleRepository
{
    Task<PolicyLifecycle?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PolicyLifecycle?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<PolicyLifecycle?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken);
    Task<PolicyLifecycle?> GetByQuoteIdForUpdateAsync(int quoteId, CancellationToken cancellationToken);
    Task<PolicyLifecycle?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<PolicyLifecycle?> GetByApplicationIdForUpdateAsync(int applicationId, CancellationToken cancellationToken);
    Task<Quote?> GetQuoteAsync(int quoteId, CancellationToken cancellationToken);
    Task<InsuranceApplication?> GetApplicationAsync(int applicationId, CancellationToken cancellationToken);
    Task AddAsync(PolicyLifecycle lifecycle, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
