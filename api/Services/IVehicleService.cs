using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IVehicleService
{
    Task<VehicleListResponse> GetAllAsync(string? keyword, int? customerId, int page, int pageSize, CancellationToken cancellationToken);
    Task<VehicleDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<VehicleDto> CreateAsync(VehicleCreateDto request, CancellationToken cancellationToken);
    Task<VehicleDto?> UpdateAsync(int id, VehicleUpdateDto request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
    Task<bool> ExistsByLicensePlateAsync(string licensePlate, int? excludingId, CancellationToken cancellationToken);
    Task<bool> ExistsByVinAsync(string vin, int? excludingId, CancellationToken cancellationToken);
    Task<bool> CustomerExistsAsync(int customerId, CancellationToken cancellationToken);
}
