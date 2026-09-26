using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IVehicleRepository
{
    Task<(IReadOnlyList<Vehicle> Items, int Total)> SearchAsync(string? keyword, int? customerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByLicensePlateAsync(string licensePlate, int? excludingId, CancellationToken cancellationToken);
    Task<bool> ExistsByVinAsync(string vin, int? excludingId, CancellationToken cancellationToken);
    Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken);
    Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
