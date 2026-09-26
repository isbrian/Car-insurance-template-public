using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IDashboardSummaryService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken);
}
