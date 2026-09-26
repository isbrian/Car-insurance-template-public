using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IClaimRepository
{
    Task<IReadOnlyList<Claim>> GetAllAsync(string? status, int? policyId, CancellationToken cancellationToken);
    Task<Claim?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Claim?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken);
    Task<Customer?> GetCustomerForUpdateAsync(int customerId, CancellationToken cancellationToken);
    Task<string?> GetLatestClaimNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddAsync(Claim claim, CancellationToken cancellationToken);
    void Remove(Claim claim);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
