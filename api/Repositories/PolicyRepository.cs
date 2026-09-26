using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class PolicyRepository(CarInsuranceDbContext dbContext) : IPolicyRepository
{
    private IQueryable<Policy> ReadQuery() => dbContext.Policies
        .AsNoTracking()
        .Include(x => x.Application)
        .Include(x => x.Payment)
        .Include(x => x.Quote);

    public async Task<(IReadOnlyList<Policy> Items, int Total)> SearchAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = ReadQuery();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(x =>
                x.PolicyNumber.Contains(normalizedKeyword) ||
                x.CustomerName.Contains(normalizedKeyword) ||
                (x.InsuredName != null && x.InsuredName.Contains(normalizedKeyword)) ||
                x.LicensePlate.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.IssuedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Policy?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<PolicyTimelineSource?> GetTimelineSourceAsync(int id, CancellationToken cancellationToken)
    {
        var policy = await ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null)
        {
            return null;
        }

        var lifecycle = await dbContext.PolicyLifecycles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApplicationId == policy.ApplicationId, cancellationToken);

        return new PolicyTimelineSource(policy, lifecycle);
    }

    public Task<Policy?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public Task<bool> ExistsForApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Policies.AnyAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public async Task<PolicyIssuanceSource?> GetIssuanceSourceAsync(
        int applicationId,
        CancellationToken cancellationToken)
    {
        var application = await dbContext.Applications
            .AsNoTracking()
            .Include(x => x.Quote)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Quote)
                .ThenInclude(x => x.Vehicle)
            .Include(x => x.Quote)
                .ThenInclude(x => x.RateVersion)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

        if (application is null)
        {
            return null;
        }

        var payment = await dbContext.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

        var lifecycleStatus = await dbContext.PolicyLifecycles
            .AsNoTracking()
            .Where(x => x.ApplicationId == applicationId)
            .Select(x => x.Status)
            .FirstOrDefaultAsync(cancellationToken);

        return new PolicyIssuanceSource(application, payment, lifecycleStatus);
    }

    public Task<string?> GetLatestPolicyNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Policies
            .AsNoTracking()
            .Where(x => x.PolicyNumber.StartsWith(prefix))
            .OrderByDescending(x => x.PolicyNumber)
            .Select(x => x.PolicyNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(Policy policy, CancellationToken cancellationToken) =>
        dbContext.Policies.AddAsync(policy, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
