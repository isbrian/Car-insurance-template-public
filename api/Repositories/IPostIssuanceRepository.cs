using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IPostIssuanceRepository
{
    Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PolicyPostIssuanceEvent>> GetEventsAsync(int policyId, CancellationToken cancellationToken);
    Task<PolicyPostIssuanceEvent?> GetEventForUpdateAsync(int policyId, string step, CancellationToken cancellationToken);
    Task AddAsync(PolicyPostIssuanceEvent item, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
