using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IEndorsementRepository
{
    Task<IReadOnlyList<Endorsement>> GetAllAsync(CancellationToken cancellationToken);
    Task<Endorsement?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Endorsement>> GetByPolicyIdAsync(int policyId, CancellationToken cancellationToken);
    Task<Endorsement?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<Policy?> GetPolicyAsync(int policyId, CancellationToken cancellationToken);
    Task<string?> GetLifecycleStatusAsync(int applicationId, CancellationToken cancellationToken);
    Task<bool> HasOpenForPolicyAsync(int policyId, CancellationToken cancellationToken);
    Task<string?> GetLatestEndorsementNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddAsync(Endorsement endorsement, CancellationToken cancellationToken);
    void Remove(Endorsement endorsement);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
