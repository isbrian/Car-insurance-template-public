using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IPaymentRepository
{
    Task<IReadOnlyList<Payment>> GetAllAsync(CancellationToken cancellationToken);
    Task<Payment?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Payment?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<Payment?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<InsuranceApplication?> GetApplicationAsync(int applicationId, CancellationToken cancellationToken);
    Task<bool> ExistsForApplicationAsync(int applicationId, CancellationToken cancellationToken);
    Task<string?> GetLatestPaymentNumberAsync(string prefix, CancellationToken cancellationToken);
    Task<IReadOnlyList<Payment>> GetPendingDueBeforeForUpdateAsync(DateTime nowUtc, CancellationToken cancellationToken);
    Task<string?> GetLifecycleStatusAsync(int applicationId, CancellationToken cancellationToken);
    Task AddAsync(Payment payment, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
