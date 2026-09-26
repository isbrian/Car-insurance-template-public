using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class PolicyLifecycleRepository(CarInsuranceDbContext dbContext) : IPolicyLifecycleRepository
{
    private IQueryable<PolicyLifecycle> ReadQuery() => dbContext.PolicyLifecycles
        .AsNoTracking()
        .Include(x => x.Quote)
        .Include(x => x.Application);

    private IQueryable<PolicyLifecycle> UpdateQuery() => dbContext.PolicyLifecycles
        .Include(x => x.Quote)
        .Include(x => x.Application);

    public Task<PolicyLifecycle?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PolicyLifecycle?> GetByIdForUpdateAsync(int id, CancellationToken cancellationToken) =>
        UpdateQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PolicyLifecycle?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.QuoteId == quoteId, cancellationToken);

    public Task<PolicyLifecycle?> GetByQuoteIdForUpdateAsync(int quoteId, CancellationToken cancellationToken)
    {
        var local = dbContext.PolicyLifecycles.Local.FirstOrDefault(x => x.QuoteId == quoteId);
        return local is not null
            ? Task.FromResult<PolicyLifecycle?>(local)
            : UpdateQuery().FirstOrDefaultAsync(x => x.QuoteId == quoteId, cancellationToken);
    }

    public Task<PolicyLifecycle?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);

    public Task<PolicyLifecycle?> GetByApplicationIdForUpdateAsync(int applicationId, CancellationToken cancellationToken)
    {
        var local = dbContext.PolicyLifecycles.Local.FirstOrDefault(x => x.ApplicationId == applicationId);
        return local is not null
            ? Task.FromResult<PolicyLifecycle?>(local)
            : UpdateQuery().FirstOrDefaultAsync(x => x.ApplicationId == applicationId, cancellationToken);
    }

    public Task<Quote?> GetQuoteAsync(int quoteId, CancellationToken cancellationToken) =>
        dbContext.Quotes.FirstOrDefaultAsync(x => x.Id == quoteId, cancellationToken);

    public Task<InsuranceApplication?> GetApplicationAsync(int applicationId, CancellationToken cancellationToken) =>
        dbContext.Applications
            .Include(x => x.Quote)
            .FirstOrDefaultAsync(x => x.Id == applicationId, cancellationToken);

    public Task AddAsync(PolicyLifecycle lifecycle, CancellationToken cancellationToken) =>
        dbContext.PolicyLifecycles.AddAsync(lifecycle, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
