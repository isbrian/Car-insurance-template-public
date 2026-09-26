namespace CarInsurance.Api.Services;

public sealed class RenewalReminderBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<RenewalReminderBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("RenewalReminder:Enabled", true))
        {
            logger.LogInformation("續保到期提醒 BackgroundService 已停用。");
            return;
        }

        var configuredInterval = configuration.GetValue("RenewalReminder:IntervalMinutes", 1440);
        var intervalMinutes = Math.Max(1, configuredInterval);
        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRenewalReminderService>();
                var result = await service.GenerateAsync(DateTime.UtcNow, stoppingToken);

                logger.LogInformation(
                    "續保到期提醒掃描完成。Candidates={CandidateCount}, Created={CreatedCount}, SkippedDuplicate={SkippedDuplicateCount}, MissingEmail={MissingEmailCount}",
                    result.CandidateCount,
                    result.CreatedCount,
                    result.SkippedDuplicateCount,
                    result.MissingEmailCount);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "續保到期提醒掃描失敗。");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
