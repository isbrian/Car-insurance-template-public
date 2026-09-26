namespace CarInsurance.Api.Services;

public interface IRenewalReminderService
{
    Task<RenewalReminderRunResult> GenerateAsync(DateTime utcNow, CancellationToken cancellationToken);
}

public sealed record RenewalReminderRunResult(
    int CandidateCount,
    int CreatedCount,
    int SkippedDuplicateCount,
    int MissingEmailCount);
