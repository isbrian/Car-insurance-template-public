using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IInsuranceCatalogRepository
{
    Task<IReadOnlyList<InsuranceType>> GetInsuranceTypesAsync(CancellationToken cancellationToken);
    Task<InsuranceType?> GetInsuranceTypeByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeHasProductsAsync(int id, CancellationToken cancellationToken);
    Task AddInsuranceTypeAsync(InsuranceType entity, CancellationToken cancellationToken);
    Task RemoveInsuranceTypeAsync(InsuranceType entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<InsuranceProduct>> GetInsuranceProductsAsync(int? insuranceTypeId, CancellationToken cancellationToken);
    Task<InsuranceProduct?> GetInsuranceProductByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceTypeExistsAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceProductCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken);
    Task<bool> InsuranceProductHasRateVersionsAsync(int id, CancellationToken cancellationToken);
    Task AddInsuranceProductAsync(InsuranceProduct entity, CancellationToken cancellationToken);
    Task RemoveInsuranceProductAsync(InsuranceProduct entity, CancellationToken cancellationToken);

    Task<IReadOnlyList<RateVersion>> GetRateVersionsAsync(int? insuranceProductId, CancellationToken cancellationToken);
    Task<RateVersion?> GetRateVersionByIdAsync(int id, CancellationToken cancellationToken);
    Task<bool> InsuranceProductExistsAsync(int id, CancellationToken cancellationToken);
    Task<bool> RateVersionExistsAsync(int insuranceProductId, string version, int? excludingId, CancellationToken cancellationToken);
    Task AddRateVersionAsync(RateVersion entity, CancellationToken cancellationToken);
    Task RemoveRateVersionAsync(RateVersion entity, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
