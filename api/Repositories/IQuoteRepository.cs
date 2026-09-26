using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IQuoteRepository
{
    Task<IReadOnlyList<Quote>> GetAllAsync(CancellationToken cancellationToken);
    Task<Quote?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<Quote?> GetForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken);
    Task<int?> GetVehicleCustomerIdAsync(int vehicleId, CancellationToken cancellationToken);
    Task<string?> GetLatestQuoteNumberAsync(string prefix, CancellationToken cancellationToken);
    Task AddAsync(Quote quote, CancellationToken cancellationToken);
    Task RemoveAsync(Quote quote, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
