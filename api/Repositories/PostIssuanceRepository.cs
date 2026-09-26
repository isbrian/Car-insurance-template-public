using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class PostIssuanceRepository(CarInsuranceDbContext dbContext) : IPostIssuanceRepository
{
    public Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken) =>
        dbContext.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == policyId, cancellationToken);

    public async Task<IReadOnlyList<PolicyPostIssuanceEvent>> GetEventsAsync(
        int policyId,
        CancellationToken cancellationToken) =>
        await dbContext.PolicyPostIssuanceEvents
            .AsNoTracking()
            .Where(x => x.PolicyId == policyId)
            .ToListAsync(cancellationToken);

    public Task<PolicyPostIssuanceEvent?> GetEventForUpdateAsync(
        int policyId,
        string step,
        CancellationToken cancellationToken) =>
        dbContext.PolicyPostIssuanceEvents
            .FirstOrDefaultAsync(x => x.PolicyId == policyId && x.Step == step, cancellationToken);

    public Task AddAsync(PolicyPostIssuanceEvent item, CancellationToken cancellationToken) =>
        dbContext.PolicyPostIssuanceEvents.AddAsync(item, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
