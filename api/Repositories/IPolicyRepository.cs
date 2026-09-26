using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public sealed record PolicyIssuanceSource(
    InsuranceApplication Application,
    Payment? Payment,
    string? LifecycleStatus);

public sealed record PolicyTimelineSource(
    Policy Policy,
    PolicyLifecycle? Lifecycle);

public interface IPolicyRepository
{
    Task<(IReadOnlyList<Policy> Items, int Total)> SearchAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
    Task<Policy?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PolicyTimelineSource?> GetTimelineSourceAsync(int id, CancellationToken cancellationToken);
    Task<Policy?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<bool> ExistsForApplicationAsync(int applicationId, CancellationToken cancellationToken);
    Task<PolicyIssuanceSource?> GetIssuanceSourceAsync(int applicationId, CancellationToken cancellationToken);
    Task<string?> GetLatestPolicyNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddAsync(Policy policy, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
