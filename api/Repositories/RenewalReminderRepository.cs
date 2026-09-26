using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class RenewalReminderRepository(CarInsuranceDbContext dbContext) : IRenewalReminderRepository
{
    public async Task<IReadOnlyList<Policy>> GetExpiringPoliciesAsync(
        DateTime startUtc,
        DateTime endExclusiveUtc,
        CancellationToken cancellationToken) =>
        await dbContext.Policies
            .AsNoTracking()
            .Where(x =>
                x.Status == "active" &&
                x.InsuranceEndDate >= startUtc &&
                x.InsuranceEndDate < endExclusiveUtc)
            .OrderBy(x => x.InsuranceEndDate)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<string>> GetExistingNotificationKeysAsync(
        IReadOnlyCollection<string> referenceIds,
        CancellationToken cancellationToken)
    {
        if (referenceIds.Count == 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var rows = await dbContext.Notifications
            .AsNoTracking()
            .Where(x =>
                x.EventType == RenewalReminderService.EventType &&
                x.ReferenceType == RenewalReminderService.ReferenceType &&
                x.ReferenceId != null &&
                referenceIds.Contains(x.ReferenceId))
            .Select(x => new { x.Channel, x.ReferenceId })
            .ToListAsync(cancellationToken);

        return rows
            .Where(x => x.ReferenceId is not null)
            .Select(x => RenewalReminderService.BuildNotificationKey(x.Channel, x.ReferenceId!))
            .ToHashSet(StringComparer.Ordinal);
    }
}
