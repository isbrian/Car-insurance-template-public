using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class ClaimService(
    IClaimRepository repository,
    IAuditLogService auditLogService) : IClaimService
{
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);

    public static class Statuses
    {
        public const string PendingIntake = "pending_intake";
        public const string Reviewing = "reviewing";
        public const string PendingDocuments = "pending_documents";
        public const string Calculating = "calculating";
        public const string PendingPayment = "pending_payment";
        public const string Closed = "closed";
        public const string Denied = "denied";
    }

    private static readonly HashSet<string> SupportedStatuses =
    [
        Statuses.PendingIntake,
        Statuses.Reviewing,
        Statuses.PendingDocuments,
        Statuses.Calculating,
        Statuses.PendingPayment,
        Statuses.Closed,
        Statuses.Denied
    ];

    private static readonly HashSet<(string From, string To)> AllowedTransitions =
    [
        (Statuses.PendingIntake, Statuses.Reviewing),
        (Statuses.Reviewing, Statuses.PendingDocuments),
        (Statuses.PendingDocuments, Statuses.Reviewing)
    ];

    public async Task<IReadOnlyList<ClaimDto>> GetAllAsync(
        string? status,
        int? policyId,
        CancellationToken cancellationToken)
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : NormalizeStatus(status);
        if (policyId is <= 0)
        {
            throw new ClaimValidationException("policyId 必須大於 0。");
        }

        return (await repository.GetAllAsync(normalizedStatus, policyId, cancellationToken))
            .Select(Map)
            .ToList();
    }

    public async Task<ClaimDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var claim = await repository.GetByIdAsync(id, cancellationToken);
        return claim is null ? null : Map(claim);
    }

    public async Task<ClaimDto> CreateAsync(
        ClaimCreateDto request,
        CancellationToken cancellationToken)
    {
        var policy = await repository.GetPolicyAsync(request.PolicyId, cancellationToken)
            ?? throw new ClaimNotFoundException($"找不到 PolicyId={request.PolicyId} 的保單。");

        ValidateAccidentDate(request.AccidentDate, policy);
        var now = DateTime.UtcNow;
        var claim = new Claim
        {
            ClaimNumber = await GenerateNumberAsync(now, cancellationToken),
            PolicyId = policy.Id,
            Status = Statuses.PendingIntake,
            AccidentDate = ToUtcStartOfBusinessDate(request.AccidentDate),
            AccidentLocation = NormalizeRequired(request.AccidentLocation, 300, "accidentLocation"),
            AccidentDescription = NormalizeRequired(request.AccidentDescription, 2000, "accidentDescription"),
            ClaimedAmount = NormalizeAmount(request.ClaimedAmount),
            LastTransition = "claim_created",
            StatusChangedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(claim, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(claim.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立理賠案件後無法重新讀取。");
    }

    public async Task<ClaimDto?> UpdateAsync(
        int id,
        ClaimUpdateDto request,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null)
        {
            return null;
        }

        EnsureNotTerminal(claim, "已結案案件不可修改。");
        ValidateAccidentDate(request.AccidentDate, claim.Policy);
        claim.AccidentDate = ToUtcStartOfBusinessDate(request.AccidentDate);
        claim.AccidentLocation = NormalizeRequired(request.AccidentLocation, 300, "accidentLocation");
        claim.AccidentDescription = NormalizeRequired(request.AccidentDescription, 2000, "accidentDescription");
        claim.ClaimedAmount = NormalizeAmount(request.ClaimedAmount);
        claim.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null)
        {
            return false;
        }

        if (!string.Equals(claim.Status, Statuses.PendingIntake, StringComparison.Ordinal))
        {
            throw new ClaimConflictException("只有 pending_intake 理賠案件可以刪除。");
        }

        repository.Remove(claim);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ClaimDto?> TransitionAsync(
        int id,
        ClaimTransitionDto request,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null)
        {
            return null;
        }

        var targetStatus = NormalizeStatus(request.TargetStatus);
        EnsureNotTerminal(claim, "已結案案件不可再轉換狀態。");

        if (!AllowedTransitions.Contains((claim.Status, targetStatus)))
        {
            throw new ClaimConflictException($"理賠案件不可由 {claim.Status} 轉為 {targetStatus}。");
        }

        if (claim.Status == Statuses.PendingIntake && targetStatus == Statuses.Reviewing)
        {
            claim.AssignedAdjuster = NormalizeRequired(request.AssignedAdjuster, 100, "assignedAdjuster");
        }
        else if (!string.IsNullOrWhiteSpace(request.AssignedAdjuster))
        {
            claim.AssignedAdjuster = NormalizeRequired(request.AssignedAdjuster, 100, "assignedAdjuster");
        }

        var now = DateTime.UtcNow;
        claim.Status = targetStatus;
        claim.LastTransition = ResolveTransition(targetStatus);
        claim.LastTransitionReason = NormalizeOptional(request.Reason, 1000);
        claim.StatusChangedAtUtc = now;
        claim.UpdatedAtUtc = now;

        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ClaimDto?> CompleteLiabilityAsync(
        int id,
        ClaimLiabilityDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null) return null;

        EnsureStatus(claim, Statuses.Reviewing, "只有 reviewing 理賠案件可以完成責任認定。");
        var liabilityDecision = NormalizeRequired(request.Decision, 500, "decision");
        if (request.LiabilityPercentage is < 0 or > 100)
        {
            throw new ClaimValidationException("liabilityPercentage 必須介於 0 到 100。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(claim);
        var now = DateTime.UtcNow;
        claim.LiabilityDecision = liabilityDecision;
        claim.LiabilityPercentage = request.LiabilityPercentage;
        claim.Status = Statuses.Calculating;
        claim.LastTransition = "claim_calculating";
        claim.LastTransitionReason = NormalizeOptional(request.Reason, 1000) ?? claim.LiabilityDecision;
        claim.StatusChangedAtUtc = now;
        claim.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "claim.liability.completed",
            claim.Id,
            beforeJson,
            SerializeDecisionSnapshot(claim),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ClaimDto?> ApproveAmountAsync(
        int id,
        ClaimApproveDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null) return null;

        EnsureStatus(claim, Statuses.Calculating, "只有 calculating 理賠案件可以核定理賠金額。");
        if (request.ApprovedAmount < 0)
        {
            throw new ClaimValidationException("approvedAmount 不可小於 0。");
        }
        if (claim.ClaimedAmount.HasValue && request.ApprovedAmount > claim.ClaimedAmount.Value)
        {
            throw new ClaimValidationException("approvedAmount 不可高於 claimedAmount。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(claim);
        var now = DateTime.UtcNow;
        claim.ApprovedAmount = request.ApprovedAmount;
        claim.Status = Statuses.PendingPayment;
        claim.LastTransition = "claim_payment_pending";
        claim.LastTransitionReason = NormalizeOptional(request.Reason, 1000);
        claim.StatusChangedAtUtc = now;
        claim.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "claim.amount.approved",
            claim.Id,
            beforeJson,
            SerializeDecisionSnapshot(claim),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ClaimDto?> CompletePaymentAsync(
        int id,
        ClaimPayDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null) return null;

        EnsureStatus(claim, Statuses.PendingPayment, "只有 pending_payment 理賠案件可以完成給付。");
        if (!claim.ApprovedAmount.HasValue)
        {
            throw new ClaimConflictException("尚未核定理賠金額，不可完成給付。");
        }

        var paymentMethod = NormalizeRequired(request.PaymentMethod, 100, "paymentMethod");
        Customer? customer = null;
        if (claim.ApprovedAmount.Value > 0m)
        {
            customer = await repository.GetCustomerForUpdateAsync(claim.Policy.CustomerId, cancellationToken)
                ?? throw new ClaimConflictException($"PolicyId={claim.PolicyId} 找不到對應客戶，無法回寫賠款紀錄等級。");
        }

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(claim);
        var now = DateTime.UtcNow;
        claim.PaymentMethod = paymentMethod;
        claim.PaidAtUtc = now;
        claim.Status = Statuses.Closed;
        claim.LastTransition = "claim_closed";
        claim.LastTransitionReason = NormalizeOptional(request.Reason, 1000);
        claim.StatusChangedAtUtc = now;
        claim.UpdatedAtUtc = now;
        if (customer is not null)
        {
            customer.ClaimRecordLevel = Math.Min(10, customer.ClaimRecordLevel + 3);
        }
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "claim.payment.completed",
            claim.Id,
            beforeJson,
            SerializeDecisionSnapshot(claim),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ClaimDto?> DenyAsync(
        int id,
        ClaimDenyDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var claim = await repository.GetForUpdateAsync(id, cancellationToken);
        if (claim is null) return null;

        EnsureStatus(claim, Statuses.Reviewing, "只有 reviewing 理賠案件可以拒賠結案。");
        var reason = NormalizeRequired(request.Reason, 1000, "reason");
        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeDecisionSnapshot(claim);
        var now = DateTime.UtcNow;
        claim.DeniedReason = reason;
        claim.Status = Statuses.Denied;
        claim.LastTransition = "claim_denied";
        claim.LastTransitionReason = reason;
        claim.StatusChangedAtUtc = now;
        claim.UpdatedAtUtc = now;
        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "claim.denied",
            claim.Id,
            beforeJson,
            SerializeDecisionSnapshot(claim),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task<string> GenerateNumberAsync(DateTime nowUtc, CancellationToken cancellationToken)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, BusinessTimeZone);
        var prefix = $"CLM-{local:yyyyMM}-";
        var latest = await repository.GetLatestClaimNumberAsync(prefix, cancellationToken);
        var next = 1;
        if (!string.IsNullOrWhiteSpace(latest) &&
            latest.Length > prefix.Length &&
            int.TryParse(latest[prefix.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static void ValidateAccidentDate(DateOnly accidentDate, Policy policy)
    {
        var start = ToBusinessDate(policy.InsuranceStartDate);
        var end = ToBusinessDate(policy.InsuranceEndDate);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone));

        if (accidentDate < start || accidentDate > end)
        {
            throw new ClaimValidationException($"accidentDate 必須落在保險期間 {start:yyyy-MM-dd} 至 {end:yyyy-MM-dd} 內。");
        }

        if (accidentDate > today)
        {
            throw new ClaimValidationException("accidentDate 不可晚於今日。");
        }
    }

    private static DateOnly ToBusinessDate(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, BusinessTimeZone));
    }

    private static DateTime ToUtcStartOfBusinessDate(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, BusinessTimeZone);
    }

    private static string ResolveTransition(string targetStatus) => targetStatus switch
    {
        Statuses.Reviewing => "claim_reviewing",
        Statuses.PendingDocuments => "claim_documents_requested",
        Statuses.Calculating => "claim_calculating",
        Statuses.PendingPayment => "claim_payment_pending",
        Statuses.Closed => "claim_closed",
        Statuses.Denied => "claim_denied",
        _ => "claim_transitioned"
    };

    private static string NormalizeStatus(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!SupportedStatuses.Contains(normalized))
        {
            throw new ClaimValidationException($"不支援的 status：{value}。");
        }
        return normalized;
    }

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ClaimValidationException($"{field} 為必填。");
        }
        if (normalized.Length > maxLength)
        {
            throw new ClaimValidationException($"{field} 不可超過 {maxLength} 字元。");
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ClaimValidationException($"reason 不可超過 {maxLength} 字元。");
        }
        return normalized;
    }

    private static decimal? NormalizeAmount(decimal? value)
    {
        if (value is < 0)
        {
            throw new ClaimValidationException("claimedAmount 不可小於 0。");
        }
        return value;
    }

    private static void EnsureNotTerminal(Claim claim, string message)
    {
        if (claim.Status is Statuses.Closed or Statuses.Denied)
        {
            throw new ClaimConflictException(message);
        }
    }

    private static void EnsureStatus(Claim claim, string expectedStatus, string message)
    {
        EnsureNotTerminal(claim, "已結案案件不可再執行理賠作業。");
        if (!string.Equals(claim.Status, expectedStatus, StringComparison.Ordinal))
        {
            throw new ClaimConflictException(message);
        }
    }

    private static string SerializeDecisionSnapshot(Claim claim) =>
        JsonSerializer.Serialize(
            new
            {
                claim.Status,
                claim.LiabilityDecision,
                claim.LiabilityPercentage,
                claim.ApprovedAmount,
                claim.PaymentMethod
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
        int claimId,
        string beforeJson,
        string afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "Claim",
                claimId.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private static ClaimDto Map(Claim claim) => new(
        claim.Id,
        claim.ClaimNumber,
        claim.PolicyId,
        claim.Policy.PolicyNumber,
        claim.Policy.CustomerName,
        claim.Policy.LicensePlate,
        claim.Status,
        ToBusinessDate(claim.AccidentDate),
        claim.AccidentLocation,
        claim.AccidentDescription,
        claim.ClaimedAmount,
        claim.LiabilityDecision,
        claim.LiabilityPercentage,
        claim.ApprovedAmount,
        claim.PaymentMethod,
        claim.PaidAtUtc,
        claim.DeniedReason,
        claim.AssignedAdjuster,
        claim.LastTransition,
        claim.LastTransitionReason,
        claim.StatusChangedAtUtc,
        claim.CreatedAtUtc,
        claim.UpdatedAtUtc);

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

public sealed class ClaimValidationException(string message) : Exception(message);
public sealed class ClaimConflictException(string message) : Exception(message);
public sealed class ClaimNotFoundException(string message) : Exception(message);
