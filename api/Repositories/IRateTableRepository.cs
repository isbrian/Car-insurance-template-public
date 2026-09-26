using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IRateTableRepository
{
    Task<RateTable?> GetByRateVersionIdAsync(int rateVersionId, CancellationToken cancellationToken);
}
