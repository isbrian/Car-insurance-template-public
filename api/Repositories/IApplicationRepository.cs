using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IApplicationRepository
{
    Task<IReadOnlyList<InsuranceApplication>> GetAllAsync(CancellationToken cancellationToken);
    Task<InsuranceApplication?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<InsuranceApplication?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken);
    Task<InsuranceApplication?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<Quote?> GetQuoteAsync(int quoteId, CancellationToken cancellationToken);
    Task<bool> ExistsForQuoteAsync(int quoteId, CancellationToken cancellationToken);
    Task<string?> GetLatestApplicationNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddAsync(InsuranceApplication application, CancellationToken cancellationToken);
    void ReplaceBeneficiaries(InsuranceApplication application, IEnumerable<ApplicationBeneficiary> beneficiaries);
    void RemoveSignature(ApplicationSignature signature);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
