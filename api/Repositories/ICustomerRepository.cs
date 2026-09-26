using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface ICustomerRepository
{
    Task<(IReadOnlyList<Customer> Items, int Total)> SearchAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByNationalIdAsync(string nationalId, int? excludingId, CancellationToken cancellationToken);
    Task AddAsync(Customer customer, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
