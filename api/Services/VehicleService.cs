using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class VehicleService(IVehicleRepository repository) : IVehicleService
{
    public async Task<VehicleListResponse> GetAllAsync(string? keyword, int? customerId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var (items, total) = await repository.SearchAsync(keyword, customerId, page, pageSize, cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new VehicleListResponse(items.Select(Map).ToList(), page, pageSize, total, totalPages);
    }

    public async Task<VehicleDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken);
        return vehicle is null ? null : Map(vehicle);
    }

    public async Task<VehicleDto> CreateAsync(VehicleCreateDto request, CancellationToken cancellationToken)
    {
        var vehicle = new Vehicle
        {
            LicensePlate = NormalizeUpper(request.LicensePlate),
            Vin = NormalizeUpper(request.Vin),
            MakeModelCode = request.MakeModelCode.Trim(),
            ManufactureDate = request.ManufactureDate,
            UsageType = request.UsageType.Trim(),
            CustomerId = request.CustomerId
        };

        await repository.AddAsync(vehicle, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetByIdAsync(vehicle.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立車輛後無法讀取資料。");
        return Map(saved);
    }

    public async Task<VehicleDto?> UpdateAsync(int id, VehicleUpdateDto request, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken);
        if (vehicle is null) return null;

        vehicle.LicensePlate = NormalizeUpper(request.LicensePlate);
        vehicle.Vin = NormalizeUpper(request.Vin);
        vehicle.MakeModelCode = request.MakeModelCode.Trim();
        vehicle.ManufactureDate = request.ManufactureDate;
        vehicle.UsageType = request.UsageType.Trim();
        vehicle.CustomerId = request.CustomerId;

        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("更新車輛後無法讀取資料。");
        return Map(saved);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(id, cancellationToken);
        if (vehicle is null) return false;
        await repository.RemoveAsync(vehicle, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public Task<bool> ExistsByLicensePlateAsync(string licensePlate, int? excludingId, CancellationToken cancellationToken) =>
        repository.ExistsByLicensePlateAsync(NormalizeUpper(licensePlate), excludingId, cancellationToken);

    public Task<bool> ExistsByVinAsync(string vin, int? excludingId, CancellationToken cancellationToken) =>
        repository.ExistsByVinAsync(NormalizeUpper(vin), excludingId, cancellationToken);

    public Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken) =>
        repository.CustomerExistsAsync(customerId, cancellationToken);

    private static VehicleDto Map(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.LicensePlate,
        MaskVin(vehicle.Vin),
        vehicle.MakeModelCode,
        vehicle.ManufactureDate,
        vehicle.UsageType,
        vehicle.CustomerId,
        vehicle.Customer.Name);

    private static string NormalizeUpper(string value) => value.Trim().ToUpperInvariant();

    private static string MaskVin(string value) => value.Length <= 4
        ? new string('*', value.Length)
        : new string('*', value.Length - 4) + value[^4..];
}
