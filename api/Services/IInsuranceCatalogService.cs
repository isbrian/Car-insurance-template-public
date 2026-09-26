using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IInsuranceCatalogService
{
    Task<IReadOnlyList<InsuranceTypeDto>> GetInsuranceTypesAsync(CancellationToken cancellationToken);
    Task<InsuranceTypeDto?> GetInsuranceTypeAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeHasProductsAsync(int id, CancellationToken cancellationToken);
    Task<InsuranceTypeDto> CreateInsuranceTypeAsync(InsuranceTypeCreateDto request, CancellationToken cancellationToken);
    Task<InsuranceTypeDto?> UpdateInsuranceTypeAsync(int id, InsuranceTypeUpdateDto request, CancellationToken cancellationToken);
    Task<bool> DeleteInsuranceTypeAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsuranceProductDto>> GetInsuranceProductsAsync(int? insuranceTypeId, CancellationToken cancellationToken);
    Task<InsuranceProductDto?> GetInsuranceProductAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeExistsAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceProductCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<bool> InsuranceProductHasRateVersionsAsync(int id, CancellationToken cancellationToken);
    Task<InsuranceProductDto> CreateInsuranceProductAsync(InsuranceProductCreateDto request, CancellationToken cancellationToken);
    Task<InsuranceProductDto?> UpdateInsuranceProductAsync(int id, InsuranceProductUpdateDto request, CancellationToken cancellationToken);
    Task<bool> DeleteInsuranceProductAsync(int id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RateVersionDto>> GetRateVersionsAsync(int? insuranceProductId, CancellationToken cancellationToken);
    Task<RateVersionDto?> GetRateVersionAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceProductExistsAsync(int id, CancellationToken cancellationToken);
    Task<bool> RateVersionExistsAsync(int insuranceProductId, string version, int? excludingId, CancellationToken cancellationToken);
    Task<RateVersionDto> CreateRateVersionAsync(RateVersionCreateDto request, CancellationToken cancellationToken);
    Task<RateVersionDto?> UpdateRateVersionAsync(int id, RateVersionUpdateDto request, CancellationToken cancellationToken);
    Task<bool> DeleteRateVersionAsync(int id, CancellationToken cancellationToken);
}
