using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class PostIssuanceService(
    IPostIssuanceRepository repository,
    ILogger<PostIssuanceService> logger) : IPostIssuanceService
{
    private const string CompletedStatus = "completed";
    private const string FailedStatus = "failed";

    private static readonly (string Step, string Title)[] Steps =
    [
        ("bureau_upload", "保發中心上傳"),
        ("e_policy_delivery", "電子保單派送"),
        ("commission_sync", "佣金回寫")
    ];

    public async Task<PolicyPostIssuanceTimelineDto> ProcessAsync(
        int policyId,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(policyId, cancellationToken)
            ?? throw new PostIssuanceNotFoundException($"找不到 Id={policyId} 的保單。");

        if (!string.Equals(policy.Status, "issued", StringComparison.Ordinal))
        {
            throw new PostIssuanceConflictException("只有 issued 狀態的正式保單可執行出單後處理。");
        }

        foreach (var step in Steps)
        {
            var existing = await repository.GetEventForUpdateAsync(policyId, step.Step, cancellationToken);
            if (existing is not null && string.Equals(existing.Status, CompletedStatus, StringComparison.Ordinal))
            {
                continue;
            }

            var now = DateTime.UtcNow;
            var item = existing ?? new PolicyPostIssuanceEvent
            {
                PolicyId = policyId,
                Step = step.Step,
                CreatedAtUtc = now
            };

            item.AttemptCount += 1;

            try
            {
                item.Detail = SimulateExternalIntegration(policy, step.Step);
                item.Status = CompletedStatus;
                item.CompletedAtUtc = now;

                if (existing is null)
                {
                    await repository.AddAsync(item, cancellationToken);
                }

                await repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                item.Status = FailedStatus;
                item.Detail = ex.Message;
                item.CompletedAtUtc = null;

                if (existing is null)
                {
                    await repository.AddAsync(item, cancellationToken);
                }

                await repository.SaveChangesAsync(cancellationToken);
                throw new PostIssuanceConflictException($"{step.Title}失敗：{ex.Message}");
            }
        }

        return await BuildTimelineAsync(policy, cancellationToken);
    }

    public async Task<PolicyPostIssuanceTimelineDto?> GetTimelineAsync(
        int policyId,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(policyId, cancellationToken);
        return policy is null ? null : await BuildTimelineAsync(policy, cancellationToken);
    }

    private string SimulateExternalIntegration(Policy policy, string step)
    {
        switch (step)
        {
            case "bureau_upload":
                logger.LogInformation(
                    "[Demo] 已模擬上傳保單 {PolicyNumber} 至保發中心／產險公司 API。",
                    policy.PolicyNumber);
                return "Demo：已模擬上傳保發中心／產險公司 API。";
            case "e_policy_delivery":
                logger.LogInformation(
                    "[Demo] 已模擬寄送電子保單 {PolicyNumber} 給被保險人。",
                    policy.PolicyNumber);
                return "Demo：已模擬電子保單派送。";
            case "commission_sync":
                logger.LogInformation(
                    "[Demo] 已模擬回寫保單 {PolicyNumber} 的業績與佣金資料。",
                    policy.PolicyNumber);
                return "Demo：已模擬業績與佣金回寫。";
            default:
                throw new InvalidOperationException($"不支援的出單後處理步驟：{step}");
        }
    }

    private async Task<PolicyPostIssuanceTimelineDto> BuildTimelineAsync(
        Policy policy,
        CancellationToken cancellationToken)
    {
        var events = await repository.GetEventsAsync(policy.Id, cancellationToken);
        var order = Steps
            .Select((item, index) => (item.Step, index))
            .ToDictionary(x => x.Step, x => x.index, StringComparer.Ordinal);

        var titles = Steps.ToDictionary(x => x.Step, x => x.Title, StringComparer.Ordinal);
        var items = events
            .OrderBy(x => order.TryGetValue(x.Step, out var index) ? index : int.MaxValue)
            .ThenBy(x => x.Id)
            .Select(x => new PolicyPostIssuanceEventDto(
                x.Id,
                policy.Id,
                policy.PolicyNumber,
                x.Step,
                titles.TryGetValue(x.Step, out var title) ? title : x.Step,
                x.Status,
                x.Detail,
                x.AttemptCount,
                x.CreatedAtUtc,
                x.CompletedAtUtc))
            .ToList();

        return new PolicyPostIssuanceTimelineDto(policy.Id, policy.PolicyNumber, items);
    }
}

public sealed class PostIssuanceNotFoundException(string message) : Exception(message);

public sealed class PostIssuanceConflictException(string message) : Exception(message);
