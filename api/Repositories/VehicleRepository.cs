using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class VehicleRepository(CarInsuranceDbContext dbContext) : IVehicleRepository
{
    public async Task<(IReadOnlyList<Vehicle> Items, int Total)> SearchAsync(string? keyword, int? customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Vehicles.AsNoTracking().Include(vehicle => vehicle.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var value = keyword.Trim();
            query = query.Where(vehicle =>
                vehicle.LicensePlate.Contains(value) ||
                vehicle.Vin.Contains(value) ||
                vehicle.MakeModelCode.Contains(value));
        }

        if (customerId.HasValue)
        {
            query = query.Where(vehicle => vehicle.CustomerId == customerId.Value);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(vehicle => vehicle.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public Task<Vehicle?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.Vehicles.Include(vehicle => vehicle.Customer)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == id, cancellationToken);

    public Task<bool> ExistsByLicensePlateAsync(string licensePlate, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.Vehicles.AnyAsync(vehicle => vehicle.LicensePlate == licensePlate &&
            (!excludingId.HasValue || vehicle.Id != excludingId.Value), cancellationToken);

    public Task<bool> ExistsByVinAsync(string vin, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.Vehicles.AnyAsync(vehicle => vehicle.Vin == vin &&
            (!excludingId.HasValue || vehicle.Id != excludingId.Value), cancellationToken);

    public Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken) =>
        dbContext.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken);

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken) =>
        dbContext.Vehicles.AddAsync(vehicle, cancellationToken).AsTask();

    public Task RemoveAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        dbContext.Vehicles.Remove(vehicle);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
