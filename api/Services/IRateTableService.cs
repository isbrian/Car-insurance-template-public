using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IRateTableService
{
    Task<RateTableDto?> GetByRateVersionIdAsync(int rateVersionId, CancellationToken cancellationToken);
}
