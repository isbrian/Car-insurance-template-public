using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class PolicyLifecycleService(IPolicyLifecycleRepository repository) : IPolicyLifecycleService
{
    public static class Statuses
    {
        public const string Quotation = "quotation";
        public const string QuoteExpired = "quote_expired";
        public const string PendingUnderwriting = "pending_underwriting";
        public const string PendingDocuments = "pending_documents";
        public const string Rejected = "rejected";
        public const string PendingPayment = "pending_payment";
        public const string Cancelled = "cancelled";
        public const string Issued = "issued";
        public const string Active = "active";
        public const string EndorsementPending = "endorsement_pending";
        public const string RenewalPending = "renewal_pending";
        public const string Terminated = "terminated";
        public const string Expired = "expired";
    }

    private static readonly HashSet<string> SupportedStatuses = new(StringComparer.Ordinal)
    {
        Statuses.Quotation,
        Statuses.QuoteExpired,
        Statuses.PendingUnderwriting,
        Statuses.PendingDocuments,
        Statuses.Rejected,
        Statuses.PendingPayment,
        Statuses.Cancelled,
        Statuses.Issued,
        Statuses.Active,
        Statuses.EndorsementPending,
        Statuses.RenewalPending,
        Statuses.Terminated,
        Statuses.Expired
    };

    private static readonly HashSet<(string From, string To)> AllowedTransitions = new()
    {
        (Statuses.Quotation, Statuses.QuoteExpired),
        (Statuses.Quotation, Statuses.PendingUnderwriting),
        (Statuses.PendingUnderwriting, Statuses.PendingDocuments),
        (Statuses.PendingDocuments, Statuses.PendingUnderwriting),
        (Statuses.PendingUnderwriting, Statuses.Rejected),
        (Statuses.PendingUnderwriting, Statuses.PendingPayment),
        (Statuses.PendingPayment, Statuses.Cancelled),
        (Statuses.PendingPayment, Statuses.Issued),
        (Statuses.Issued, Statuses.Active),
        (Statuses.Active, Statuses.EndorsementPending),
        (Statuses.EndorsementPending, Statuses.Active),
        (Statuses.Active, Statuses.RenewalPending),
        (Statuses.RenewalPending, Statuses.Quotation),
        (Statuses.Active, Statuses.Terminated),
        (Statuses.Active, Statuses.Expired)
    };

    public async Task<PolicyLifecycleDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (lifecycle is null)
        {
            return null;
        }

        await SynchronizeQuoteExpiryAsync(lifecycle, cancellationToken);
        return await ReloadAndMapByIdAsync(id, cancellationToken);
    }

    public async Task<PolicyLifecycleDto?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByQuoteIdForUpdateAsync(quoteId, cancellationToken);
        if (lifecycle is null)
        {
            return await EnsureForQuoteAsync(quoteId, cancellationToken);
        }

        await SynchronizeQuoteExpiryAsync(lifecycle, cancellationToken);
        return await ReloadAndMapByQuoteAsync(quoteId, cancellationToken);
    }

    public async Task<PolicyLifecycleDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdForUpdateAsync(applicationId, cancellationToken);
        if (lifecycle is null)
        {
            return null;
        }

        await SynchronizeQuoteExpiryAsync(lifecycle, cancellationToken);
        return await ReloadAndMapByApplicationAsync(applicationId, cancellationToken);
    }

    public async Task<PolicyLifecycleDto?> EnsureForQuoteAsync(int quoteId, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByQuoteIdForUpdateAsync(quoteId, cancellationToken);
        if (existing is not null)
        {
            await SynchronizeQuoteExpiryAsync(existing, cancellationToken);
            return await ReloadAndMapByQuoteAsync(quoteId, cancellationToken);
        }

        var quote = await repository.GetQuoteAsync(quoteId, cancellationToken);
        if (quote is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var expired = now > quote.ValidUntilUtc;
        var lifecycle = new PolicyLifecycle
        {
            QuoteId = quote.Id,
            Quote = quote,
            Status = expired ? Statuses.QuoteExpired : Statuses.Quotation,
            LastTransition = expired ? "quote_expired" : "quote_created",
            LastTransitionReason = expired ? "報價已超過有效期限。" : "建立報價生命週期。",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(lifecycle, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapByQuoteAsync(quoteId, cancellationToken)
            ?? throw new InvalidOperationException($"建立 QuoteId={quoteId} 的保單生命週期後無法重新讀取。");
    }

    public async Task<PolicyLifecycleDto> AttachApplicationAndSubmitAsync(
        int quoteId,
        int applicationId,
        CancellationToken cancellationToken)
    {
        await PrepareApplicationSubmissionAsync(quoteId, applicationId, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapByApplicationAsync(applicationId, cancellationToken)
            ?? throw new InvalidOperationException($"ApplicationId={applicationId} 送出後無法重新讀取生命週期。");
    }

    public async Task PrepareApplicationSubmissionAsync(
        int quoteId,
        int applicationId,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByQuoteIdForUpdateAsync(quoteId, cancellationToken);
        if (lifecycle is null)
        {
            var quote = await repository.GetQuoteAsync(quoteId, cancellationToken)
                ?? throw new PolicyLifecycleNotFoundException($"找不到 QuoteId={quoteId} 的報價。");
            var now = DateTime.UtcNow;
            lifecycle = new PolicyLifecycle
            {
                QuoteId = quote.Id,
                Quote = quote,
                Status = now > quote.ValidUntilUtc ? Statuses.QuoteExpired : Statuses.Quotation,
                LastTransition = now > quote.ValidUntilUtc ? "quote_expired" : "quote_created",
                LastTransitionReason = now > quote.ValidUntilUtc ? "報價已超過有效期限。" : "建立報價生命週期。",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            await repository.AddAsync(lifecycle, cancellationToken);
        }

        if (lifecycle.Status == Statuses.Quotation && DateTime.UtcNow > lifecycle.Quote.ValidUntilUtc)
        {
            ApplyTransition(lifecycle, Statuses.QuoteExpired, "quote_expired", "報價已超過有效期限。", DateTime.UtcNow);
        }

        if (lifecycle.ApplicationId.HasValue && lifecycle.ApplicationId.Value != applicationId)
        {
            throw new PolicyLifecycleConflictException(
                $"QuoteId={quoteId} 的生命週期已綁定 ApplicationId={lifecycle.ApplicationId.Value}。");
        }

        lifecycle.ApplicationId = applicationId;
        if (lifecycle.Status == Statuses.PendingUnderwriting)
        {
            return;
        }

        ApplyTransition(lifecycle, Statuses.PendingUnderwriting, "application_submitted", "投保申請已送出，進入核保。", DateTime.UtcNow);
    }

    public async Task<PolicyLifecycleDto?> TransitionAsync(
        int id,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByIdForUpdateAsync(id, cancellationToken);
        if (lifecycle is null)
        {
            return null;
        }

        await SynchronizeQuoteExpiryAsync(lifecycle, cancellationToken);
        ApplyTransition(lifecycle, NormalizeStatus(targetStatus), NormalizeTransition(transition), NormalizeReason(reason), DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapByIdAsync(id, cancellationToken);
    }

    public async Task<PolicyLifecycleDto?> TransitionByApplicationAsync(
        int applicationId,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdForUpdateAsync(applicationId, cancellationToken);
        if (lifecycle is null)
        {
            return null;
        }

        await SynchronizeQuoteExpiryAsync(lifecycle, cancellationToken);
        ApplyTransition(lifecycle, NormalizeStatus(targetStatus), NormalizeTransition(transition), NormalizeReason(reason), DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapByApplicationAsync(applicationId, cancellationToken);
    }

    public async Task<PolicyLifecycleDto?> SyncUnderwritingStatusAsync(
        int applicationId,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        await PrepareUnderwritingStatusAsync(applicationId, targetStatus, transition, reason, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await ReloadAndMapByApplicationAsync(applicationId, cancellationToken);
    }

    public async Task PrepareUnderwritingStatusAsync(
        int applicationId,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdForUpdateAsync(applicationId, cancellationToken);
        if (lifecycle is null)
        {
            var application = await repository.GetApplicationAsync(applicationId, cancellationToken)
                ?? throw new PolicyLifecycleNotFoundException($"找不到 ApplicationId={applicationId} 的投保申請。");
            await PrepareApplicationSubmissionAsync(application.QuoteId, applicationId, cancellationToken);
            lifecycle = await repository.GetByQuoteIdForUpdateAsync(application.QuoteId, cancellationToken)
                ?? throw new InvalidOperationException($"ApplicationId={applicationId} 無法建立保單生命週期。");
        }

        var normalizedTarget = NormalizeStatus(targetStatus);
        if (normalizedTarget is not (Statuses.PendingUnderwriting or Statuses.PendingDocuments or Statuses.PendingPayment or Statuses.Rejected))
        {
            throw new PolicyLifecycleValidationException("核保整合只能同步 pending_underwriting、pending_documents、pending_payment 或 rejected。");
        }

        if (lifecycle.Status == normalizedTarget)
        {
            return;
        }

        if (lifecycle.Status is not (Statuses.PendingUnderwriting or Statuses.PendingDocuments))
        {
            // 核保同步可能在案件已進入繳費／出單後重跑；不可把正式生命週期倒退。
            return;
        }

        var now = DateTime.UtcNow;
        var normalizedTransition = NormalizeTransition(transition);
        var normalizedReason = NormalizeReason(reason);

        if (lifecycle.Status == Statuses.PendingDocuments &&
            normalizedTarget is Statuses.PendingPayment or Statuses.Rejected)
        {
            ApplyTransition(lifecycle, Statuses.PendingUnderwriting, "documents_resubmitted", "補件完成，重新進入核保。", now);
        }

        if (lifecycle.Status != normalizedTarget)
        {
            ApplyTransition(lifecycle, normalizedTarget, normalizedTransition, normalizedReason, now);
        }
    }

    public async Task PreparePaymentStatusAsync(
        int applicationId,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdForUpdateAsync(applicationId, cancellationToken)
            ?? throw new PolicyLifecycleNotFoundException($"找不到 ApplicationId={applicationId} 的保單生命週期。");

        var normalizedTarget = NormalizeStatus(targetStatus);
        if (normalizedTarget is not (Statuses.Issued or Statuses.Cancelled))
        {
            throw new PolicyLifecycleValidationException("付款整合只能同步 issued 或 cancelled。");
        }

        ApplyTransition(
            lifecycle,
            normalizedTarget,
            NormalizeTransition(transition),
            NormalizeReason(reason),
            DateTime.UtcNow);
    }

    public async Task PrepareEndorsementStatusAsync(
        int applicationId,
        string targetStatus,
        string transition,
        string? reason,
        CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdForUpdateAsync(applicationId, cancellationToken)
            ?? throw new PolicyLifecycleNotFoundException($"找不到 ApplicationId={applicationId} 的保單生命週期。");

        var normalizedTarget = NormalizeStatus(targetStatus);
        if (normalizedTarget is not (Statuses.EndorsementPending or Statuses.Active))
        {
            throw new PolicyLifecycleValidationException("批改整合只能同步 endorsement_pending 或 active。");
        }

        ApplyTransition(
            lifecycle,
            normalizedTarget,
            NormalizeTransition(transition),
            NormalizeReason(reason),
            DateTime.UtcNow);
    }

    private async Task SynchronizeQuoteExpiryAsync(PolicyLifecycle lifecycle, CancellationToken cancellationToken)
    {
        if (lifecycle.Status != Statuses.Quotation ||
            lifecycle.ApplicationId.HasValue ||
            DateTime.UtcNow <= lifecycle.Quote.ValidUntilUtc)
        {
            return;
        }

        ApplyTransition(lifecycle, Statuses.QuoteExpired, "quote_expired", "報價已超過有效期限。", DateTime.UtcNow);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static void ApplyTransition(
        PolicyLifecycle lifecycle,
        string targetStatus,
        string transition,
        string? reason,
        DateTime now)
    {
        if (!SupportedStatuses.Contains(targetStatus))
        {
            throw new PolicyLifecycleValidationException($"不支援的 targetStatus：{targetStatus}。");
        }

        if (lifecycle.Status == targetStatus || !AllowedTransitions.Contains((lifecycle.Status, targetStatus)))
        {
            throw new PolicyLifecycleConflictException($"保單生命週期不可由 {lifecycle.Status} 轉為 {targetStatus}。");
        }

        lifecycle.Status = targetStatus;
        lifecycle.LastTransition = transition;
        lifecycle.LastTransitionReason = reason;
        lifecycle.UpdatedAtUtc = now;
    }

    private static string NormalizeStatus(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || !SupportedStatuses.Contains(normalized))
        {
            throw new PolicyLifecycleValidationException($"不支援的 targetStatus：{value}。");
        }

        return normalized;
    }

    private static string NormalizeTransition(string value)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new PolicyLifecycleValidationException("transition 為必填。");
        }

        if (normalized.Length > 80)
        {
            throw new PolicyLifecycleValidationException("transition 不可超過 80 字元。");
        }

        return normalized;
    }

    private static string? NormalizeReason(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (normalized?.Length > 500)
        {
            throw new PolicyLifecycleValidationException("reason 不可超過 500 字元。");
        }

        return normalized;
    }

    private async Task<PolicyLifecycleDto?> ReloadAndMapByIdAsync(int id, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByIdAsync(id, cancellationToken);
        return lifecycle is null ? null : Map(lifecycle);
    }

    private async Task<PolicyLifecycleDto?> ReloadAndMapByQuoteAsync(int quoteId, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByQuoteIdAsync(quoteId, cancellationToken);
        return lifecycle is null ? null : Map(lifecycle);
    }

    private async Task<PolicyLifecycleDto?> ReloadAndMapByApplicationAsync(int applicationId, CancellationToken cancellationToken)
    {
        var lifecycle = await repository.GetByApplicationIdAsync(applicationId, cancellationToken);
        return lifecycle is null ? null : Map(lifecycle);
    }

    private static PolicyLifecycleDto Map(PolicyLifecycle lifecycle) => new(
        lifecycle.Id,
        lifecycle.QuoteId,
        lifecycle.Quote.QuoteNumber,
        lifecycle.ApplicationId,
        lifecycle.Application?.ApplicationNumber,
        lifecycle.Status,
        lifecycle.LastTransition,
        lifecycle.LastTransitionReason,
        lifecycle.CreatedAtUtc,
        lifecycle.UpdatedAtUtc);
}

public sealed class PolicyLifecycleValidationException(string message) : Exception(message);

public sealed class PolicyLifecycleConflictException(string message) : Exception(message);

public sealed class PolicyLifecycleNotFoundException(string message) : Exception(message);
