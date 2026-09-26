using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class InsuranceCatalogService(IInsuranceCatalogRepository repository) : IInsuranceCatalogService
{
    public async Task<IReadOnlyList<InsuranceTypeDto>> GetInsuranceTypesAsync(CancellationToken cancellationToken) =>
        (await repository.GetInsuranceTypesAsync(cancellationToken)).Select(Map).ToList();

    public async Task<InsuranceTypeDto?> GetInsuranceTypeAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceTypeByIdAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public Task<bool> InsuranceTypeCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) =>
        repository.InsuranceTypeCodeExistsAsync(NormalizeCode(code), excludingId, cancellationToken);

    public Task<bool> InsuranceTypeHasProductsAsync(int id, CancellationToken cancellationToken) =>
        repository.InsuranceTypeHasProductsAsync(id, cancellationToken);

    public async Task<InsuranceTypeDto> CreateInsuranceTypeAsync(InsuranceTypeCreateDto request, CancellationToken cancellationToken)
    {
        var entity = new InsuranceType
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.AddInsuranceTypeAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<InsuranceTypeDto?> UpdateInsuranceTypeAsync(int id, InsuranceTypeUpdateDto request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceTypeByIdAsync(id, cancellationToken);
        if (entity is null) return null;
        entity.Code = NormalizeCode(request.Code);
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.IsActive = request.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteInsuranceTypeAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceTypeByIdAsync(id, cancellationToken);
        if (entity is null) return false;
        await repository.RemoveInsuranceTypeAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<InsuranceProductDto>> GetInsuranceProductsAsync(int? insuranceTypeId, CancellationToken cancellationToken) =>
        (await repository.GetInsuranceProductsAsync(insuranceTypeId, cancellationToken)).Select(Map).ToList();

    public async Task<InsuranceProductDto?> GetInsuranceProductAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceProductByIdAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public Task<bool> InsuranceTypeExistsAsync(int id, CancellationToken cancellationToken) =>
        repository.InsuranceTypeExistsAsync(id, cancellationToken);

    public Task<bool> InsuranceProductCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) =>
        repository.InsuranceProductCodeExistsAsync(NormalizeCode(code), excludingId, cancellationToken);

    public Task<bool> InsuranceProductHasRateVersionsAsync(int id, CancellationToken cancellationToken) =>
        repository.InsuranceProductHasRateVersionsAsync(id, cancellationToken);

    public async Task<InsuranceProductDto> CreateInsuranceProductAsync(InsuranceProductCreateDto request, CancellationToken cancellationToken)
    {
        var entity = new InsuranceProduct
        {
            InsuranceTypeId = request.InsuranceTypeId,
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.AddInsuranceProductAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetInsuranceProductByIdAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立商品方案後無法讀取資料。");
        return Map(saved);
    }

    public async Task<InsuranceProductDto?> UpdateInsuranceProductAsync(int id, InsuranceProductUpdateDto request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceProductByIdAsync(id, cancellationToken);
        if (entity is null) return null;
        entity.InsuranceTypeId = request.InsuranceTypeId;
        entity.Code = NormalizeCode(request.Code);
        entity.Name = request.Name.Trim();
        entity.Description = NormalizeOptional(request.Description);
        entity.IsActive = request.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetInsuranceProductByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("更新商品方案後無法讀取資料。");
        return Map(saved);
    }

    public async Task<bool> DeleteInsuranceProductAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetInsuranceProductByIdAsync(id, cancellationToken);
        if (entity is null) return false;
        await repository.RemoveInsuranceProductAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RateVersionDto>> GetRateVersionsAsync(int? insuranceProductId, CancellationToken cancellationToken) =>
        (await repository.GetRateVersionsAsync(insuranceProductId, cancellationToken)).Select(Map).ToList();

    public async Task<RateVersionDto?> GetRateVersionAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetRateVersionByIdAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public Task<bool> InsuranceProductExistsAsync(int id, CancellationToken cancellationToken) =>
        repository.InsuranceProductExistsAsync(id, cancellationToken);

    public Task<bool> RateVersionExistsAsync(int insuranceProductId, string version, int? excludingId, CancellationToken cancellationToken) =>
        repository.RateVersionExistsAsync(insuranceProductId, NormalizeVersion(version), excludingId, cancellationToken);

    public async Task<RateVersionDto> CreateRateVersionAsync(RateVersionCreateDto request, CancellationToken cancellationToken)
    {
        var entity = new RateVersion
        {
            InsuranceProductId = request.InsuranceProductId,
            Version = NormalizeVersion(request.Version),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Confidence = NormalizeConfidence(request.Confidence),
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.AddRateVersionAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetRateVersionByIdAsync(entity.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立費率版本後無法讀取資料。");
        return Map(saved);
    }

    public async Task<RateVersionDto?> UpdateRateVersionAsync(int id, RateVersionUpdateDto request, CancellationToken cancellationToken)
    {
        var entity = await repository.GetRateVersionByIdAsync(id, cancellationToken);
        if (entity is null) return null;
        entity.InsuranceProductId = request.InsuranceProductId;
        entity.Version = NormalizeVersion(request.Version);
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.Confidence = NormalizeConfidence(request.Confidence);
        entity.IsActive = request.IsActive;
        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetRateVersionByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("更新費率版本後無法讀取資料。");
        return Map(saved);
    }

    public async Task<bool> DeleteRateVersionAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await repository.GetRateVersionByIdAsync(id, cancellationToken);
        if (entity is null) return false;
        await repository.RemoveRateVersionAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static InsuranceTypeDto Map(InsuranceType entity) =>
        new(entity.Id, entity.Code, entity.Name, entity.Description, entity.IsActive, entity.CreatedAtUtc);

    private static InsuranceProductDto Map(InsuranceProduct entity) =>
        new(entity.Id, entity.InsuranceTypeId, entity.InsuranceType.Code, entity.InsuranceType.Name, entity.Code, entity.Name, entity.Description, entity.IsActive, entity.CreatedAtUtc);

    private static RateVersionDto Map(RateVersion entity) =>
        new(entity.Id, entity.InsuranceProductId, entity.InsuranceProduct.Code, entity.InsuranceProduct.Name, entity.Version, entity.EffectiveFrom, entity.EffectiveTo, entity.Confidence, entity.IsActive, entity.CreatedAtUtc);

    private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();
    private static string NormalizeVersion(string value) => value.Trim();
    private static string NormalizeConfidence(string value) => value.Trim().ToLowerInvariant();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
