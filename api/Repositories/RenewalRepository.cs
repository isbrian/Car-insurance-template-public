using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class RenewalRepository(CarInsuranceDbContext dbContext) : IRenewalRepository
{
    public async Task<IReadOnlyList<RenewalPolicySource>> GetEligibleAsync(
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var policies = await dbContext.Policies
            .AsNoTracking()
            .Include(x => x.Quote)
            .Where(x =>
                x.InsuranceEndDate >= startUtc &&
                x.InsuranceEndDate <= endUtc)
            .OrderBy(x => x.InsuranceEndDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (policies.Count == 0)
        {
            return [];
        }

        var applicationIds = policies.Select(x => x.ApplicationId).ToArray();
        var policyIds = policies.Select(x => x.Id).ToArray();

        var lifecycles = await dbContext.PolicyLifecycles
            .AsNoTracking()
            .Where(x => x.ApplicationId.HasValue && applicationIds.Contains(x.ApplicationId.Value))
            .ToDictionaryAsync(x => x.ApplicationId!.Value, cancellationToken);

        var renewalQuotes = await dbContext.Quotes
            .AsNoTracking()
            .Where(x => x.RenewalSourcePolicyId.HasValue && policyIds.Contains(x.RenewalSourcePolicyId.Value))
            .Select(x => new { PolicyId = x.RenewalSourcePolicyId!.Value, QuoteId = x.Id })
            .ToDictionaryAsync(x => x.PolicyId, x => x.QuoteId, cancellationToken);

        return policies
            .Select(policy => new RenewalPolicySource(
                policy,
                lifecycles.GetValueOrDefault(policy.ApplicationId),
                renewalQuotes.TryGetValue(policy.Id, out var quoteId) ? quoteId : null))
            .ToList();
    }

    public async Task<RenewalPolicySource?> GetSourceByPolicyIdAsync(
        int policyId,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.Policies
            .AsNoTracking()
            .Include(x => x.Quote)
            .FirstOrDefaultAsync(x => x.Id == policyId, cancellationToken);
        if (policy is null)
        {
            return null;
        }

        var lifecycle = await dbContext.PolicyLifecycles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApplicationId == policy.ApplicationId, cancellationToken);

        var renewalQuoteId = await dbContext.Quotes
            .AsNoTracking()
            .Where(x => x.RenewalSourcePolicyId == policyId)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return new RenewalPolicySource(policy, lifecycle, renewalQuoteId);
    }

    public Task<int?> GetCustomerClaimRecordLevelAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => (int?)x.ClaimRecordLevel)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<int> CountPaidClaimsAsync(
        int customerId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Claims
            .AsNoTracking()
            .Where(x =>
                x.Policy.CustomerId == customerId &&
                x.Status == ClaimService.Statuses.Closed &&
                x.PaidAtUtc != null &&
                x.AccidentDate >= fromUtc &&
                x.AccidentDate <= toUtc)
            .Select(x => x.ApprovedAmount)
            .ToListAsync(cancellationToken);

        return candidates.Count(amount => amount is > 0m);
    }

    public Task<string?> GetLatestQuoteNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .AsNoTracking()
            .Where(x => x.QuoteNumber.StartsWith(prefix))
            .OrderByDescending(x => x.QuoteNumber)
            .Select(x => x.QuoteNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddQuoteAsync(Quote quote, CancellationToken cancellationToken) =>
        dbContext.Quotes.AddAsync(quote, cancellationToken).AsTask();

    public Task<Quote?> GetQuoteByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
