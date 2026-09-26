using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class ClaimRepository(CarInsuranceDbContext dbContext) : IClaimRepository
{
    private IQueryable<Claim> ReadQuery() => dbContext.Claims
        .AsNoTracking()
        .Include(x => x.Policy);

    private IQueryable<Claim> UpdateQuery() => dbContext.Claims
        .Include(x => x.Policy);

    public async Task<IReadOnlyList<Claim>> GetAllAsync(
        string? status,
        int? policyId,
        CancellationToken cancellationToken)
    {
        var query = ReadQuery();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (policyId.HasValue)
        {
            query = query.Where(x => x.PolicyId == policyId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Claim?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Claim?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        UpdateQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken) =>
        dbContext.Policies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == policyId, cancellationToken);

    public Task<Customer?> GetCustomerForUpdateAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.FirstOrDefaultAsync(x => x.Id == customerId, cancellationToken);

    public Task<string?> GetLatestClaimNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Claims
            .AsNoTracking()
            .Where(x => x.ClaimNumber.StartsWith(prefix))
            .OrderByDescending(x => x.ClaimNumber)
            .Select(x => x.ClaimNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Claim claim, CancellationToken cancellationToken) =>
        dbContext.Claims.AddAsync(claim, cancellationToken).AsTask();

    public void Remove(Claim claim) => dbContext.Claims.Remove(claim);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
