using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class InsuranceCatalogRepository(CarInsuranceDbContext dbContext) : IInsuranceCatalogRepository
{
    public async Task<IReadOnlyList<InsuranceType>> GetInsuranceTypesAsync(CancellationToken cancellationToken) =>
        await dbContext.InsuranceTypes.AsNoTracking().OrderBy(x => x.Id).ToListAsync(cancellationToken);

    public Task<InsuranceType?> GetInsuranceTypeByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.InsuranceTypes.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> InsuranceTypeCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.InsuranceTypes.AnyAsync(x => x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<bool> InsuranceTypeHasProductsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.InsuranceProducts.AnyAsync(x => x.InsuranceTypeId == id, cancellationToken);

    public Task AddInsuranceTypeAsync(InsuranceType entity, CancellationToken cancellationToken) =>
        dbContext.InsuranceTypes.AddAsync(entity, cancellationToken).AsTask();

    public Task RemoveInsuranceTypeAsync(InsuranceType entity, CancellationToken cancellationToken)
    {
        dbContext.InsuranceTypes.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<InsuranceProduct>> GetInsuranceProductsAsync(int? insuranceTypeId, CancellationToken cancellationToken)
    {
        var query = dbContext.InsuranceProducts.AsNoTracking().Include(x => x.InsuranceType).AsQueryable();
        if (insuranceTypeId.HasValue) query = query.Where(x => x.InsuranceTypeId == insuranceTypeId.Value);
        return await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<InsuranceProduct?> GetInsuranceProductByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.InsuranceProducts.Include(x => x.InsuranceType).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> InsuranceTypeExistsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.InsuranceTypes.AnyAsync(x => x.Id == id, cancellationToken);

    public Task<bool> InsuranceProductCodeExistsAsync(string code, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.InsuranceProducts.AnyAsync(x => x.Code == code && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task<bool> InsuranceProductHasRateVersionsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.RateVersions.AnyAsync(x => x.InsuranceProductId == id, cancellationToken);

    public Task AddInsuranceProductAsync(InsuranceProduct entity, CancellationToken cancellationToken) =>
        dbContext.InsuranceProducts.AddAsync(entity, cancellationToken).AsTask();

    public Task RemoveInsuranceProductAsync(InsuranceProduct entity, CancellationToken cancellationToken)
    {
        dbContext.InsuranceProducts.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<RateVersion>> GetRateVersionsAsync(int? insuranceProductId, CancellationToken cancellationToken)
    {
        var query = dbContext.RateVersions.AsNoTracking().Include(x => x.InsuranceProduct).AsQueryable();
        if (insuranceProductId.HasValue) query = query.Where(x => x.InsuranceProductId == insuranceProductId.Value);
        return await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public Task<RateVersion?> GetRateVersionByIdAsync(int id, CancellationToken cancellationToken) =>
        dbContext.RateVersions.Include(x => x.InsuranceProduct).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> InsuranceProductExistsAsync(int id, CancellationToken cancellationToken) =>
        dbContext.InsuranceProducts.AnyAsync(x => x.Id == id, cancellationToken);

    public Task<bool> RateVersionExistsAsync(int insuranceProductId, string version, int? excludingId, CancellationToken cancellationToken) =>
        dbContext.RateVersions.AnyAsync(x => x.InsuranceProductId == insuranceProductId && x.Version == version && (!excludingId.HasValue || x.Id != excludingId.Value), cancellationToken);

    public Task AddRateVersionAsync(RateVersion entity, CancellationToken cancellationToken) =>
        dbContext.RateVersions.AddAsync(entity, cancellationToken).AsTask();

    public Task RemoveRateVersionAsync(RateVersion entity, CancellationToken cancellationToken)
    {
        dbContext.RateVersions.Remove(entity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
