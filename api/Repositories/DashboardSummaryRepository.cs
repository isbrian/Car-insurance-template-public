using CarInsurance.Api.Data;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class DashboardSummaryRepository(CarInsuranceDbContext dbContext) : IDashboardSummaryRepository
{
    public Task<int> CountCustomersAsync(CancellationToken cancellationToken) =>
        dbContext.Customers
            .AsNoTracking()
            .CountAsync(cancellationToken);

    public Task<int> CountActivePoliciesAsync(CancellationToken cancellationToken) =>
        dbContext.PolicyLifecycles
            .AsNoTracking()
            .CountAsync(
                x => x.Status == PolicyLifecycleService.Statuses.Active,
                cancellationToken);

    public Task<int> CountPendingUnderwritingAsync(CancellationToken cancellationToken) =>
        dbContext.UnderwritingCases
            .AsNoTracking()
            .CountAsync(
                x => x.Status == "pending_review" || x.Status == "pending_documents",
                cancellationToken);

    public Task<int> CountPendingClaimsAsync(CancellationToken cancellationToken) =>
        dbContext.Claims
            .AsNoTracking()
            .CountAsync(
                x => x.Status != ClaimService.Statuses.Closed &&
                     x.Status != ClaimService.Statuses.Denied,
                cancellationToken);
}
