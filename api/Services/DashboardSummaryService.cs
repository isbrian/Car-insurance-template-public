using CarInsurance.Api.Dtos;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class DashboardSummaryService(IDashboardSummaryRepository repository) : IDashboardSummaryService
{
    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var customerTotal = await repository.CountCustomersAsync(cancellationToken);
        var activePolicyTotal = await repository.CountActivePoliciesAsync(cancellationToken);
        var pendingUnderwritingTotal = await repository.CountPendingUnderwritingAsync(cancellationToken);
        var pendingClaimTotal = await repository.CountPendingClaimsAsync(cancellationToken);

        return new DashboardSummaryDto(
            customerTotal,
            activePolicyTotal,
            pendingUnderwritingTotal,
            pendingClaimTotal);
    }
}
