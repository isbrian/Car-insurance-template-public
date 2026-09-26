using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class PaymentService(
    IPaymentRepository repository,
    IPolicyLifecycleService policyLifecycleService,
    IAuditLogService auditLogService) : IPaymentService
{
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> PaymentFrequencies = new(StringComparer.Ordinal)
    {
        "annual",
        "semiannual",
        "quarterly"
    };

    private static readonly HashSet<string> PaymentMethods = new(StringComparer.Ordinal)
    {
        "credit_card",
        "atm",
        "convenience_store",
        "cash_counter"
    };

    public async Task<IReadOnlyList<PaymentDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var payments = await repository.GetAllAsync(cancellationToken);
        var result = new List<PaymentDto>(payments.Count);
        foreach (var payment in payments)
        {
            result.Add(await MapAsync(payment, cancellationToken));
        }

        return result;
    }

    public async Task<PaymentDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var payment = await repository.GetByIdAsync(id, cancellationToken);
        return payment is null ? null : await MapAsync(payment, cancellationToken);
    }

    public async Task<PaymentDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken)
    {
        var payment = await repository.GetByApplicationIdAsync(applicationId, cancellationToken);
        return payment is null ? null : await MapAsync(payment, cancellationToken);
    }

    public async Task<PaymentDto> CreateAsync(PaymentCreateDto request, CancellationToken cancellationToken)
    {
        var application = await repository.GetApplicationAsync(request.ApplicationId, cancellationToken)
            ?? throw new PaymentNotFoundException($"找不到 ApplicationId={request.ApplicationId} 的投保申請。");

        if (!string.Equals(application.Status, "submitted", StringComparison.Ordinal))
        {
            throw new PaymentConflictException("投保申請尚未送出，不可建立付款資料。");
        }

        var lifecycle = await policyLifecycleService.GetByApplicationIdAsync(application.Id, cancellationToken);
        if (lifecycle is null || lifecycle.Status != PolicyLifecycleService.Statuses.PendingPayment)
        {
            throw new PaymentConflictException("保單生命週期必須為 pending_payment 才可建立付款資料。");
        }

        if (await repository.ExistsForApplicationAsync(application.Id, cancellationToken))
        {
            throw new PaymentConflictException($"ApplicationId={application.Id} 已有付款資料。");
        }

        var frequency = NormalizePaymentFrequency(request.PaymentFrequency);
        var method = NormalizePaymentMethod(request.PaymentMethod);
        var now = DateTime.UtcNow;
        var payment = new Payment
        {
            ApplicationId = application.Id,
            PaymentNumber = await GeneratePaymentNumberAsync(now, cancellationToken),
            Status = "pending",
            PaymentFrequency = frequency,
            PaymentMethod = method,
            AmountDue = application.Quote.TotalPremium,
            InstallmentCount = GetInstallmentCount(frequency),
            DueAtUtc = now.AddDays(7),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddAsync(payment, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(payment.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立付款資料後無法重新讀取。");
    }

    public async Task<PaymentDto?> UpdateAsync(
        int id,
        PaymentUpdateDto request,
        CancellationToken cancellationToken)
    {
        var payment = await repository.GetForUpdateAsync(id, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        EnsurePending(payment, "已完成或已逾期的付款資料不可修改。");
        await EnsureLifecyclePendingPaymentAsync(payment.ApplicationId, cancellationToken);

        var frequency = NormalizePaymentFrequency(request.PaymentFrequency);
        payment.PaymentFrequency = frequency;
        payment.InstallmentCount = GetInstallmentCount(frequency);
        payment.PaymentMethod = NormalizePaymentMethod(request.PaymentMethod);
        payment.Note = NormalizeOptional(request.Note, 1000, "note");
        payment.UpdatedAtUtc = DateTime.UtcNow;

        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PaymentDto?> ConfirmAsync(
        int id,
        PaymentConfirmDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var payment = await repository.GetForUpdateAsync(id, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        EnsurePending(payment, "只有待繳費資料可以確認收款。");
        await policyLifecycleService.PreparePaymentStatusAsync(
            payment.ApplicationId,
            PolicyLifecycleService.Statuses.Issued,
            "payment_confirmed",
            "收款確認完成，進入已出單狀態。",
            cancellationToken);

        var actor = RequireAuditActor(auditActor);
        var beforeJson = SerializeConfirmationSnapshot(payment);
        var now = DateTime.UtcNow;
        payment.Status = "paid";
        payment.PaidAtUtc = now;
        payment.ReferenceNumber = NormalizeOptional(request.ReferenceNumber, 100, "referenceNumber");
        payment.Note = NormalizeOptional(request.Note, 1000, "note");
        payment.UpdatedAtUtc = now;

        await repository.SaveChangesAsync(cancellationToken);
        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "payment.confirmed",
                "Payment",
                payment.Id.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                SerializeConfirmationSnapshot(payment),
                actor.SourceIp),
            cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PaymentDto?> MarkOverdueAsync(int id, CancellationToken cancellationToken)
    {
        var payment = await repository.GetForUpdateAsync(id, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        EnsurePending(payment, "只有待繳費資料可以標記逾期。");
        var now = DateTime.UtcNow;
        if (now <= payment.DueAtUtc)
        {
            throw new PaymentConflictException("付款期限尚未到，不可標記為逾期未繳。");
        }

        await PrepareOverdueAsync(payment, now, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<PaymentSyncOverdueResultDto> SyncOverdueAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var candidates = await repository.GetPendingDueBeforeForUpdateAsync(now, cancellationToken);
        var overdueCount = 0;

        foreach (var payment in candidates)
        {
            var lifecycle = await policyLifecycleService.GetByApplicationIdAsync(payment.ApplicationId, cancellationToken);
            if (lifecycle?.Status != PolicyLifecycleService.Statuses.PendingPayment)
            {
                continue;
            }

            await PrepareOverdueAsync(payment, now, cancellationToken);
            overdueCount++;
        }

        if (overdueCount > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return new PaymentSyncOverdueResultDto(candidates.Count, overdueCount);
    }

    private async Task PrepareOverdueAsync(Payment payment, DateTime now, CancellationToken cancellationToken)
    {
        await policyLifecycleService.PreparePaymentStatusAsync(
            payment.ApplicationId,
            PolicyLifecycleService.Statuses.Cancelled,
            "payment_overdue",
            "付款期限已過且尚未收款，取消案件。",
            cancellationToken);

        payment.Status = "overdue";
        payment.CancelledAtUtc = now;
        payment.UpdatedAtUtc = now;
    }

    private async Task EnsureLifecyclePendingPaymentAsync(int applicationId, CancellationToken cancellationToken)
    {
        var lifecycle = await policyLifecycleService.GetByApplicationIdAsync(applicationId, cancellationToken);
        if (lifecycle is null || lifecycle.Status != PolicyLifecycleService.Statuses.PendingPayment)
        {
            throw new PaymentConflictException("保單生命週期已不在 pending_payment，不可修改付款資料。");
        }
    }

    private static void EnsurePending(Payment payment, string message)
    {
        if (payment.Status != "pending")
        {
            throw new PaymentConflictException(message);
        }
    }

    private async Task<string> GeneratePaymentNumberAsync(DateTime now, CancellationToken cancellationToken)
    {
        var prefix = $"PAY-{now:yyyyMM}-";
        var latest = await repository.GetLatestPaymentNumberAsync(prefix, cancellationToken);
        var next = 1;
        if (!string.IsNullOrWhiteSpace(latest) &&
            int.TryParse(latest[prefix.Length..], out var sequence))
        {
            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static string NormalizePaymentFrequency(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PaymentFrequencies.Contains(normalized))
        {
            throw new PaymentValidationException("paymentFrequency 只接受 annual、semiannual 或 quarterly。");
        }

        return normalized;
    }

    private static string NormalizePaymentMethod(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!PaymentMethods.Contains(normalized))
        {
            throw new PaymentValidationException("paymentMethod 只接受 credit_card、atm、convenience_store 或 cash_counter。");
        }

        return normalized;
    }

    private static int GetInstallmentCount(string frequency) => frequency switch
    {
        "annual" => 1,
        "semiannual" => 2,
        "quarterly" => 4,
        _ => throw new PaymentValidationException($"不支援的 paymentFrequency：{frequency}。")
    };

    private static string? NormalizeOptional(string? value, int maxLength, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new PaymentValidationException($"{field} 不可超過 {maxLength} 字元。");
        }

        return normalized;
    }

    private static string SerializeConfirmationSnapshot(Payment payment) =>
        JsonSerializer.Serialize(
            new
            {
                payment.Status,
                payment.ReferenceNumber,
                payment.Note
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

    private async Task<PaymentDto> MapAsync(Payment payment, CancellationToken cancellationToken)
    {
        var lifecycleStatus = await repository.GetLifecycleStatusAsync(payment.ApplicationId, cancellationToken);
        var quote = payment.Application.Quote;
        return new PaymentDto(
            payment.Id,
            payment.PaymentNumber,
            payment.ApplicationId,
            payment.Application.ApplicationNumber,
            quote.Id,
            quote.QuoteNumber,
            quote.CustomerId,
            quote.Customer.Name,
            quote.VehicleId,
            quote.Vehicle.LicensePlate,
            payment.Status,
            payment.PaymentFrequency,
            payment.PaymentMethod,
            payment.AmountDue,
            payment.InstallmentCount,
            payment.DueAtUtc,
            payment.PaidAtUtc,
            payment.CancelledAtUtc,
            payment.ReferenceNumber,
            payment.Note,
            payment.CreatedAtUtc,
            payment.UpdatedAtUtc,
            lifecycleStatus);
    }
}

public sealed class PaymentValidationException(string message) : Exception(message);

public sealed class PaymentConflictException(string message) : Exception(message);

public sealed class PaymentNotFoundException(string message) : Exception(message);
