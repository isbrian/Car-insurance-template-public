using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class RenewalReminderService(
    IRenewalReminderRepository repository,
    INotificationService notificationService,
    ILogger<RenewalReminderService> logger) : IRenewalReminderService
{
    public const string EventType = "renewal_reminder";
    public const string ReferenceType = "renewal_reminder";

    private const string SmsChannel = "sms";
    private const string EmailChannel = "email";

    private static readonly HashSet<int> ReminderDays = [60, 30, 7];
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

    public async Task<RenewalReminderRunResult> GenerateAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var businessDate = GetBusinessDate(utcNow);
        var startUtc = ToUtcStartOfBusinessDate(businessDate.AddDays(7));
        var endExclusiveUtc = ToUtcStartOfBusinessDate(businessDate.AddDays(61));

        var policies = await repository.GetExpiringPoliciesAsync(
            startUtc,
            endExclusiveUtc,
            cancellationToken);

        var candidates = policies
            .Select(policy => new ReminderCandidate(
                policy,
                FromUtcToBusinessDate(policy.InsuranceEndDate)))
            .Select(candidate => candidate with
            {
                DaysUntilExpiry = candidate.EndDate.DayNumber - businessDate.DayNumber
            })
            .Where(candidate => ReminderDays.Contains(candidate.DaysUntilExpiry))
            .ToList();

        var referenceIds = candidates
            .Select(candidate => BuildReferenceId(candidate.Policy.Id, candidate.DaysUntilExpiry))
            .ToArray();
        var existingKeys = (await repository.GetExistingNotificationKeysAsync(
            referenceIds,
            cancellationToken)).ToHashSet(StringComparer.Ordinal);

        var createdCount = 0;
        var skippedDuplicateCount = 0;
        var missingEmailCount = 0;

        foreach (var candidate in candidates)
        {
            var referenceId = BuildReferenceId(candidate.Policy.Id, candidate.DaysUntilExpiry);
            var title = $"續保提醒：保單 {candidate.Policy.PolicyNumber} 將於 {candidate.EndDate:yyyy-MM-dd} 到期";
            var message = $"保單 {candidate.Policy.PolicyNumber} 將於 {candidate.EndDate:yyyy-MM-dd} 到期，距到期日 {candidate.DaysUntilExpiry} 天，請安排續保作業。";

            createdCount += await CreateIfMissingAsync(
                existingKeys,
                SmsChannel,
                candidate.Policy.CustomerPhone,
                title,
                message,
                referenceId,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(candidate.Policy.CustomerEmail))
            {
                missingEmailCount += 1;
                logger.LogWarning(
                    "續保提醒未建立 Email：PolicyId={PolicyId}, PolicyNumber={PolicyNumber} 缺少 CustomerEmail。",
                    candidate.Policy.Id,
                    candidate.Policy.PolicyNumber);
                continue;
            }

            createdCount += await CreateIfMissingAsync(
                existingKeys,
                EmailChannel,
                candidate.Policy.CustomerEmail,
                title,
                message,
                referenceId,
                cancellationToken);
        }

        var expectedDeliveries = candidates.Sum(candidate =>
            string.IsNullOrWhiteSpace(candidate.Policy.CustomerEmail) ? 1 : 2);
        skippedDuplicateCount = expectedDeliveries - createdCount;

        return new RenewalReminderRunResult(
            candidates.Count,
            createdCount,
            skippedDuplicateCount,
            missingEmailCount);
    }

    private async Task<int> CreateIfMissingAsync(
        HashSet<string> existingKeys,
        string channel,
        string recipient,
        string title,
        string message,
        string referenceId,
        CancellationToken cancellationToken)
    {
        var key = BuildNotificationKey(channel, referenceId);
        if (existingKeys.Contains(key))
        {
            return 0;
        }

        _ = await notificationService.CreateAsync(
            new NotificationCreateDto(
                EventType,
                channel,
                recipient,
                title,
                message,
                ReferenceType,
                referenceId),
            cancellationToken);

        existingKeys.Add(key);
        return 1;
    }

    public static string BuildReferenceId(int policyId, int daysUntilExpiry) =>
        $"policy:{policyId}:days:{daysUntilExpiry}";

    public static string BuildNotificationKey(string channel, string referenceId) =>
        $"{channel}|{referenceId}";

    private static DateOnly GetBusinessDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
            BusinessTimeZone));

    private static DateTime ToUtcStartOfBusinessDate(DateOnly businessDate)
    {
        var localMidnight = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessTimeZone);
    }

    private static DateOnly FromUtcToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            BusinessTimeZone));

    private sealed record ReminderCandidate(
        Policy Policy,
        DateOnly EndDate,
        int DaysUntilExpiry = 0);
}
