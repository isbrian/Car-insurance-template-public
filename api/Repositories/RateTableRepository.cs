using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class RateTableRepository(CarInsuranceDbContext dbContext) : IRateTableRepository
{
    public Task<RateTable?> GetByRateVersionIdAsync(int rateVersionId, CancellationToken cancellationToken) =>
        dbContext.RateTables.AsNoTracking().FirstOrDefaultAsync(x => x.RateVersionId == rateVersionId, cancellationToken);
}
