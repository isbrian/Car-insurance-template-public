using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class ApplicationRepository(CarInsuranceDbContext dbContext) : IApplicationRepository
{
    private IQueryable<InsuranceApplication> ReadQuery() => dbContext.Applications
        .AsNoTracking()
        .AsSplitQuery()
        .Include(x => x.Quote)
            .ThenInclude(x => x.Customer)
        .Include(x => x.Quote)
            .ThenInclude(x => x.Vehicle)
        .Include(x => x.Beneficiaries)
        .Include(x => x.Signatures)
        .Include(x => x.Documents);

    public async Task<IReadOnlyList<InsuranceApplication>> GetAllAsync(CancellationToken cancellationToken) =>
        await ReadQuery()
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<InsuranceApplication?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<InsuranceApplication?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken) =>
        ReadQuery().FirstOrDefaultAsync(x => x.QuoteId == quoteId, cancellationToken);

    public Task<InsuranceApplication?> GetForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Applications
            .AsSplitQuery()
            .Include(x => x.Quote)
                .ThenInclude(x => x.Customer)
            .Include(x => x.Quote)
                .ThenInclude(x => x.Vehicle)
            .Include(x => x.Beneficiaries)
            .Include(x => x.Signatures)
            .Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Quote?> GetQuoteAsync(int quoteId, CancellationToken cancellationToken) =>
        dbContext.Quotes
            .AsNoTracking()
            .Include(x => x.Customer)
            .Include(x => x.Vehicle)
            .FirstOrDefaultAsync(x => x.Id == quoteId, cancellationToken);

    public Task<bool> ExistsForQuoteAsync(int quoteId, CancellationToken cancellationToken) =>
        dbContext.Applications.AnyAsync(x => x.QuoteId == quoteId, cancellationToken);

    public Task<string?> GetLatestApplicationNumberAsync(string prefix, CancellationToken cancellationToken) =>
        dbContext.Applications
            .AsNoTracking()
            .Where(x => x.ApplicationNumber.StartsWith(prefix))
            .OrderByDescending(x => x.ApplicationNumber)
            .Select(x => x.ApplicationNumber)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(InsuranceApplication application, CancellationToken cancellationToken) =>
        dbContext.Applications.AddAsync(application, cancellationToken).AsTask();

    public void ReplaceBeneficiaries(
        InsuranceApplication application,
        IEnumerable<ApplicationBeneficiary> beneficiaries)
    {
        dbContext.ApplicationBeneficiaries.RemoveRange(application.Beneficiaries);
        application.Beneficiaries.Clear();

        foreach (var beneficiary in beneficiaries)
        {
            application.Beneficiaries.Add(beneficiary);
        }
    }

    public void RemoveSignature(ApplicationSignature signature) =>
        dbContext.ApplicationSignatures.Remove(signature);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
