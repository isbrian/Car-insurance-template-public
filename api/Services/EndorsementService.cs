using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class EndorsementService(
    IEndorsementRepository repository,
    IPolicyLifecycleService policyLifecycleService,
    IPremiumCalculationService premiumCalculationService,
    IAuditLogService auditLogService) : IEndorsementService
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<EndorsementDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<EndorsementDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetByIdAsync(id, cancellationToken);
        return endorsement is null ? null : Map(endorsement);
    }

    public async Task<IReadOnlyList<EndorsementDto>> GetByPolicyIdAsync(int policyId, CancellationToken cancellationToken) =>
        (await repository.GetByPolicyIdAsync(policyId, cancellationToken)).Select(Map).ToList();

    public async Task<EndorsementDto> CreateAsync(
        EndorsementCreateDto request,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(request.PolicyId, cancellationToken)
            ?? throw new EndorsementNotFoundException($"找不到 PolicyId={request.PolicyId} 的保單。");

        var lifecycleStatus = await repository.GetLifecycleStatusAsync(policy.ApplicationId, cancellationToken);
        if (!string.Equals(lifecycleStatus, PolicyLifecycleService.Statuses.Active, StringComparison.Ordinal))
        {
            throw new EndorsementConflictException("保單生命週期必須為 active 才可建立批改單。");
        }

        if (await repository.HasOpenForPolicyAsync(policy.Id, cancellationToken))
        {
            throw new EndorsementConflictException($"PolicyId={policy.Id} 已有尚未結案的批改單。");
        }

        var effectiveDate = NormalizeEffectiveDate(request.RequestedEffectiveDate, policy);
        var now = DateTime.UtcNow;
        var endorsement = new Endorsement
        {
            EndorsementNumber = await GenerateNumberAsync(now, cancellationToken),
            PolicyId = policy.Id,
            EndorsementType = NormalizeRequired(request.EndorsementType, 80, "endorsementType"),
            RequestedEffectiveDate = effectiveDate,
            Reason = NormalizeRequired(request.Reason, 1000, "reason"),
            ChangeSummary = NormalizeRequired(request.ChangeSummary, 2000, "changeSummary"),
            Status = "draft",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(endorsement, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(endorsement.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立批改單後無法重新讀取。");
    }

    public async Task<EndorsementDto?> UpdateAsync(
        int id,
        EndorsementUpdateDto request,
        CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return null;
        }

        EnsureStatus(endorsement, "draft", "只有 draft 批改單可以修改。");
        endorsement.EndorsementType = NormalizeRequired(request.EndorsementType, 80, "endorsementType");
        endorsement.RequestedEffectiveDate = NormalizeEffectiveDate(request.RequestedEffectiveDate, endorsement.Policy);
        endorsement.Reason = NormalizeRequired(request.Reason, 1000, "reason");
        endorsement.ChangeSummary = NormalizeRequired(request.ChangeSummary, 2000, "changeSummary");
        endorsement.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<EndorsementDto?> CalculateAdjustmentAsync(
        int id,
        PremiumCalculationRequestDto request,
        CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return null;
        }

        if (endorsement.Status is not ("draft" or "submitted" or "reviewing"))
        {
            throw new EndorsementConflictException("只有尚未結案的批改單可以計算退補費。");
        }

        var policy = endorsement.Policy;
        if (request.RateVersionId != policy.Quote.RateVersionId)
        {
            throw new EndorsementValidationException("rateVersionId 必須與原保單報價使用的費率版本一致。");
        }

        var coverageStart = ToBusinessDate(policy.InsuranceStartDate);
        var coverageEnd = ToBusinessDate(policy.InsuranceEndDate);
        var effectiveDate = endorsement.RequestedEffectiveDate.Date;
        var coverageDays = (coverageEnd - coverageStart).Days + 1;
        var remainingDays = (coverageEnd - effectiveDate).Days + 1;
        if (coverageDays <= 0 || remainingDays <= 0 || remainingDays > coverageDays)
        {
            throw new EndorsementConflictException("保單期間或批改生效日資料不合理，無法計算退補費。");
        }

        var recalculated = await premiumCalculationService.CalculateAsync(request, cancellationToken);
        var originalPremium = RoundCurrency(policy.TotalPremium);
        var recalculatedPremium = RoundCurrency(recalculated.TotalPremium);
        var unexpiredRatio = (decimal)remainingDays / coverageDays;
        var adjustmentAmount = RoundCurrency((recalculatedPremium - originalPremium) * unexpiredRatio);

        endorsement.OriginalPremiumMinorUnits = ToMinorUnits(originalPremium);
        endorsement.RecalculatedPremiumMinorUnits = ToMinorUnits(recalculatedPremium);
        endorsement.AdjustmentAmountMinorUnits = ToMinorUnits(adjustmentAmount);
        endorsement.CoverageDays = coverageDays;
        endorsement.RemainingDays = remainingDays;
        endorsement.AdjustmentCalculationRequestJson = JsonSerializer.Serialize(request);
        endorsement.AdjustmentCalculatedAtUtc = DateTime.UtcNow;
        endorsement.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return false;
        }

        EnsureStatus(endorsement, "draft", "只有 draft 批改單可以刪除。");
        repository.Remove(endorsement);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EndorsementDto?> SubmitAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return null;
        }

        EnsureStatus(endorsement, "draft", "只有 draft 批改單可以送出申請。");
        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeTransitionSnapshot(endorsement);
        await policyLifecycleService.PrepareEndorsementStatusAsync(
            endorsement.Policy.ApplicationId,
            PolicyLifecycleService.Statuses.EndorsementPending,
            "endorsement_submitted",
            $"批改單 {endorsement.EndorsementNumber} 已送出申請。",
            cancellationToken);

        var now = DateTime.UtcNow;
        endorsement.Status = "submitted";
        endorsement.SubmittedAtUtc = now;
        endorsement.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "endorsement.submitted",
            endorsement.Id,
            beforeJson,
            SerializeTransitionSnapshot(endorsement),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<EndorsementDto?> ReviewAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return null;
        }

        EnsureStatus(endorsement, "submitted", "只有 submitted 批改單可以進入審核。");
        var lifecycleStatus = await repository.GetLifecycleStatusAsync(endorsement.Policy.ApplicationId, cancellationToken);
        if (!string.Equals(lifecycleStatus, PolicyLifecycleService.Statuses.EndorsementPending, StringComparison.Ordinal))
        {
            throw new EndorsementConflictException("保單生命週期必須為 endorsement_pending 才可進入批改審核。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeTransitionSnapshot(endorsement);
        var now = DateTime.UtcNow;
        endorsement.Status = "reviewing";
        endorsement.ReviewingAtUtc = now;
        endorsement.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "endorsement.reviewing",
            endorsement.Id,
            beforeJson,
            SerializeTransitionSnapshot(endorsement),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public Task<EndorsementDto?> ApproveAsync(
        int id,
        EndorsementDecisionDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken) =>
        DecideAsync(id, request, auditActor, "approved", "endorsement_approved", "endorsement.approved", cancellationToken);

    public Task<EndorsementDto?> RejectAsync(
        int id,
        EndorsementDecisionDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken) =>
        DecideAsync(id, request, auditActor, "returned", "endorsement_returned", "endorsement.returned", cancellationToken);

    private async Task<EndorsementDto?> DecideAsync(
        int id,
        EndorsementDecisionDto request,
        AuditActorContext auditActor,
        string targetStatus,
        string transition,
        string auditAction,
        CancellationToken cancellationToken)
    {
        var endorsement = await repository.GetForUpdateAsync(id, cancellationToken);
        if (endorsement is null)
        {
            return null;
        }

        EnsureStatus(endorsement, "reviewing", "只有 reviewing 批改單可以核准或退回。");
        var opinion = NormalizeRequired(request.ReviewOpinion, 1000, "reviewOpinion");
        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeTransitionSnapshot(endorsement);

        await policyLifecycleService.PrepareEndorsementStatusAsync(
            endorsement.Policy.ApplicationId,
            PolicyLifecycleService.Statuses.Active,
            transition,
            $"批改單 {endorsement.EndorsementNumber} 已{(targetStatus == "approved" ? "核准" : "退回")}。",
            cancellationToken);

        var now = DateTime.UtcNow;
        endorsement.Status = targetStatus;
        endorsement.ReviewOpinion = opinion;
        endorsement.DecidedAtUtc = now;
        endorsement.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            auditAction,
            endorsement.Id,
            beforeJson,
            SerializeTransitionSnapshot(endorsement),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<string> GenerateNumberAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var businessDate = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, BusinessTimeZone);
        var prefix = $"END-{businessDate:yyyyMM}-";
        var latest = await repository.GetLatestEndorsementNumberAsync(prefix, cancellationToken);
        var next = 1;
        if (!string.IsNullOrWhiteSpace(latest) &&
            latest.Length > prefix.Length &&
            int.TryParse(latest[prefix.Length..], out var sequence))
        {
            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static DateTime NormalizeEffectiveDate(DateTime value, Policy policy)
    {
        var date = value.Date;
        var startDate = ToBusinessDate(policy.InsuranceStartDate);
        var endDate = ToBusinessDate(policy.InsuranceEndDate);
        if (date < startDate || date > endDate)
        {
            throw new EndorsementValidationException("requestedEffectiveDate 必須落在保險期間內。");
        }

        return DateTime.SpecifyKind(date, DateTimeKind.Unspecified);
    }

    private static DateTime ToBusinessDate(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone).Date;
    }

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new EndorsementValidationException($"{field} 為必填欄位。");
        }

        if (normalized.Length > maxLength)
        {
            throw new EndorsementValidationException($"{field} 不可超過 {maxLength} 字元。");
        }

        return normalized;
    }

    private static void EnsureStatus(Endorsement endorsement, string expected, string message)
    {
        if (!string.Equals(endorsement.Status, expected, StringComparison.Ordinal))
        {
            throw new EndorsementConflictException(message);
        }
    }

    private static string SerializeTransitionSnapshot(Endorsement endorsement) =>
        JsonSerializer.Serialize(
            new
            {
                endorsement.Status,
                endorsement.ReviewOpinion
            },
            AuditJsonOptions);

    private static AuditActorContext RequireAuditActor(AuditActorContext auditActor)
    {
        var actor = auditActor.Actor.Trim();
        var role = auditActor.Role.Trim();
        var sourceIp = auditActor.SourceIp.Trim();
        if (actor.Length == 0 || role.Length == 0 || sourceIp.Length == 0)
        {
            throw new InvalidOperationException("缺少可信的稽核操作人、角色或來源 IP。");
        }

        return new AuditActorContext(actor, role, sourceIp);
    }

    private Task WriteAuditAsync(
        AuditActorContext actor,
        string action,
        int endorsementId,
        string beforeJson,
        string afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "Endorsement",
                endorsementId.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private static EndorsementDto Map(Endorsement x)
    {
        var originalPremium = FromMinorUnits(x.OriginalPremiumMinorUnits);
        var recalculatedPremium = FromMinorUnits(x.RecalculatedPremiumMinorUnits);
        var adjustmentAmount = FromMinorUnits(x.AdjustmentAmountMinorUnits);
        decimal? ratio = x.CoverageDays is > 0 && x.RemainingDays.HasValue
            ? (decimal)x.RemainingDays.Value / x.CoverageDays.Value
            : null;
        var adjustmentType = adjustmentAmount switch
        {
            > 0m => "additional",
            < 0m => "refund",
            0m => "none",
            _ => null
        };

        return new EndorsementDto(
            x.Id,
            x.EndorsementNumber,
            x.PolicyId,
            x.Policy.PolicyNumber,
            x.EndorsementType,
            x.RequestedEffectiveDate,
            x.Reason,
            x.ChangeSummary,
            x.Status,
            x.ReviewOpinion,
            originalPremium,
            recalculatedPremium,
            adjustmentAmount,
            adjustmentType,
            x.CoverageDays,
            x.RemainingDays,
            ratio,
            x.AdjustmentCalculatedAtUtc,
            x.SubmittedAtUtc,
            x.ReviewingAtUtc,
            x.DecidedAtUtc,
            x.CreatedAtUtc,
            x.UpdatedAtUtc);
    }

    private static long ToMinorUnits(decimal amount) =>
        checked((long)Math.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));

    private static decimal? FromMinorUnits(long? amount) =>
        amount.HasValue ? amount.Value / 100m : null;

    private static decimal RoundCurrency(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");
        }
    }
}
