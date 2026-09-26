using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public interface IRenewalReminderRepository
{
    Task<IReadOnlyList<Policy>> GetExpiringPoliciesAsync(
        DateTime startUtc,
        DateTime endExclusiveUtc,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetExistingNotificationKeysAsync(
        IReadOnlyCollection<string> referenceIds,
        CancellationToken cancellationToken);
}
