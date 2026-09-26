using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class EndorsementRepository(CarInsuranceDbContext dbContext) : IEndorsementRepository
{
    private IQueryable<Endorsement> ReadQuery() => dbContext.Endorsements
        .AsNoTracking()
        .Include(x => x.Policy)
            .ThenInclude(x => x.Quote);

    private IQueryable<Endorsement> UpdateQuery() => dbContext.Endorsements
        .Include(x => x.Policy)
            .ThenInclude(x => x.Quote);

    public async Task<IReadOnlyList<Endorsement>> GetAllAsync(CancellationToken cancellationToken) =>
        await ReadQuery()
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Endorsement?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Endorsement>> GetByPolicyIdAsync(int policyId, CancellationToken cancellationToken) =>
        await ReadQuery()
            .Where(x => x.PolicyId == policyId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<Endorsement?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        UpdateQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken) =>
        dbContext.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == policyId, cancellationToken);

    public Task<string?> GetLifecycleStatusAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.PolicyLifecycles
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOpenForPolicyAsync(int policyId, CancellationToken cancellationToken) =>
        dbContext.Endorsements.AnyAsync(
            x => x.PolicyId == policyId &&
                 (x.Status == "draft" || x.Status == "submitted" || x.Status == "reviewing"),
            cancellationToken);

    public Task<string?> GetLatestEndorsementNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Endorsements
            .AsNoTracking()
            .Where(x => x.EndorsementNumber.StartsWith(prefix))
            .OrderByDescending(x => x.EndorsementNumber)
            .Select(x => x.EndorsementNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Endorsement endorsement, CancellationToken cancellationToken) =>
        dbContext.Endorsements.AddAsync(endorsement, cancellationToken).AsTask();

    public void Remove(Endorsement endorsement) => dbContext.Endorsements.Remove(endorsement);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
