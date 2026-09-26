using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class PolicyService(
    IPolicyRepository repository,
    IPolicyPdfGenerator pdfGenerator,
    IPolicyDocumentStorage documentStorage,
    IConfiguration configuration,
    IAuditLogService auditLogService) : IPolicyService
{
    private const string DefaultTemplateVersion = "policy-v1.0";
    private static readonly JsonSerializerOptions AuditJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = ResolveBusinessTimeZone();

    public async Task<PolicyDto> IssueAsync(
        int applicationId,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var source = await repository.GetIssuanceSourceAsync(applicationId, cancellationToken)
            ?? throw new PolicyNotFoundException($"找不到 ApplicationId={applicationId} 的投保申請。");

        if (!string.Equals(source.Application.Status, "submitted", StringComparison.Ordinal))
        {
            throw new PolicyConflictException("投保申請尚未送出，不可出單。");
        }

        if (source.Payment is null)
        {
            throw new PolicyConflictException("投保申請尚未建立付款資料，不可出單。");
        }

        if (!string.Equals(source.Payment.Status, "paid", StringComparison.Ordinal))
        {
            throw new PolicyConflictException("付款狀態必須為 paid 才可出單。");
        }

        if (!string.Equals(source.LifecycleStatus, PolicyLifecycleService.Statuses.Issued, StringComparison.Ordinal))
        {
            throw new PolicyConflictException("保單生命週期必須為 issued 才可出單。");
        }

        if (await repository.ExistsForApplicationAsync(applicationId, cancellationToken))
        {
            throw new PolicyConflictException($"ApplicationId={applicationId} 已完成出單。");
        }

        var actor = RequireAuditActor(auditActor);
        var now = DateTime.UtcNow;
        var businessDate = FromUtcToBusinessDate(now);
        var quote = source.Application.Quote;
        var customer = quote.Customer;
        var vehicle = quote.Vehicle;
        var templateVersion = NormalizeTemplateVersion(configuration["PolicyIssuance:TemplateVersion"]);
        var policyNumber = await GeneratePolicyNumberAsync(businessDate, cancellationToken);
        var insuranceEndDate = quote.InsuranceStartDate.AddYears(1).AddDays(-1);
        var storedFileName = $"{policyNumber}.pdf";

        var pdf = pdfGenerator.Generate(new PolicyPdfData(
            policyNumber,
            templateVersion,
            now,
            source.Application.ApplicationNumber,
            quote.QuoteNumber,
            source.Payment.PaymentNumber,
            customer.Name,
            source.Application.InsuredName,
            vehicle.LicensePlate,
            vehicle.Vin,
            vehicle.MakeModelCode,
            vehicle.UsageType,
            FromUtcToBusinessDate(quote.InsuranceStartDate),
            FromUtcToBusinessDate(insuranceEndDate),
            quote.TotalPremium,
            source.Payment.PaymentFrequency,
            source.Payment.PaymentMethod,
            quote.SalesAgent,
            quote.Channel,
            quote.RateVersion.Version));

        var stored = await documentStorage.SaveAsync(
            applicationId,
            storedFileName,
            pdf,
            cancellationToken);

        var policy = new Policy
        {
            PolicyNumber = policyNumber,
            ApplicationId = source.Application.Id,
            PaymentId = source.Payment.Id,
            QuoteId = quote.Id,
            CustomerId = quote.CustomerId,
            VehicleId = quote.VehicleId,
            Status = PolicyLifecycleService.Statuses.Issued,
            InsuranceStartDate = quote.InsuranceStartDate,
            InsuranceEndDate = insuranceEndDate,
            TotalPremium = quote.TotalPremium,
            PaymentFrequency = source.Payment.PaymentFrequency,
            PaymentMethod = source.Payment.PaymentMethod,
            CustomerName = customer.Name,
            CustomerNationalId = customer.NationalId,
            CustomerPhone = customer.Phone,
            CustomerEmail = customer.Email,
            CustomerAddress = customer.Address,
            LicensePlate = vehicle.LicensePlate,
            Vin = vehicle.Vin,
            MakeModelCode = vehicle.MakeModelCode,
            UsageType = vehicle.UsageType,
            InsuredName = source.Application.InsuredName,
            InsuredNationalId = source.Application.InsuredNationalId,
            SalesAgent = quote.SalesAgent,
            Channel = quote.Channel,
            RateVersion = quote.RateVersion.Version,
            TemplateVersion = templateVersion,
            PdfStoredFileName = stored.StoredFileName,
            PdfContentType = "application/pdf",
            PdfSizeBytes = stored.SizeBytes,
            PdfSha256 = stored.Sha256,
            IssuedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            await repository.AddAsync(policy, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await documentStorage.DeleteAsync(applicationId, stored.StoredFileName, CancellationToken.None);
            throw;
        }

        await auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                "policy.issued",
                "Policy",
                policy.Id.ToString(CultureInfo.InvariantCulture),
                null,
                SerializeIssuanceSnapshot(policy),
                actor.SourceIp),
            cancellationToken);

        return await GetByIdAsync(policy.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立保單後無法重新讀取。");
    }

    public async Task<PolicyListResponse> SearchAsync(
        string? keyword,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedKeyword = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToLowerInvariant();
        var (items, total) = await repository.SearchAsync(
            normalizedKeyword,
            normalizedStatus,
            page,
            pageSize,
            cancellationToken);

        return new PolicyListResponse(
            items.Select(MapListItem).ToList(),
            total,
            page,
            pageSize);
    }

    public async Task<PolicyDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(id, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<PolicyDetailsDto?> GetDetailsAsync(int id, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(id, cancellationToken);
        return policy is null ? null : MapDetails(policy);
    }

    public async Task<PolicyTimelineDto?> GetTimelineAsync(int id, CancellationToken cancellationToken)
    {
        var source = await repository.GetTimelineSourceAsync(id, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var policy = source.Policy;
        var events = new List<PolicyTimelineEventDto>();
        if (policy.Application.SubmittedAtUtc.HasValue)
        {
            events.Add(new PolicyTimelineEventDto(
                "application_submitted",
                "投保申請送出",
                "completed",
                $"投保申請 {policy.Application.ApplicationNumber} 已送出。",
                policy.Application.SubmittedAtUtc.Value));
        }

        events.Add(new PolicyTimelineEventDto(
            "payment_created",
            "繳費資料建立",
            policy.Payment.Status,
            $"付款單號 {policy.Payment.PaymentNumber}。",
            policy.Payment.CreatedAtUtc));

        if (policy.Payment.PaidAtUtc.HasValue)
        {
            events.Add(new PolicyTimelineEventDto(
                "payment_paid",
                "收款完成",
                "paid",
                $"付款方式：{policy.Payment.PaymentMethod}。",
                policy.Payment.PaidAtUtc.Value));
        }

        events.Add(new PolicyTimelineEventDto(
            "policy_issued",
            "正式出單",
            policy.Status,
            $"產生保單號 {policy.PolicyNumber}。",
            policy.IssuedAtUtc));

        if (source.Lifecycle is not null && source.Lifecycle.UpdatedAtUtc != policy.IssuedAtUtc)
        {
            events.Add(new PolicyTimelineEventDto(
                "lifecycle_latest",
                "保單生命週期最後異動",
                source.Lifecycle.Status,
                BuildLifecycleDescription(source.Lifecycle),
                source.Lifecycle.UpdatedAtUtc));
        }

        return new PolicyTimelineDto(
            policy.Id,
            policy.PolicyNumber,
            source.Lifecycle?.Status,
            events.OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Key).ToList());
    }

    public async Task<PolicyDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByApplicationIdAsync(applicationId, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<PolicyDocumentContentDto?> GetDocumentAsync(int id, CancellationToken cancellationToken)
    {
        var policy = await repository.GetByIdAsync(id, cancellationToken);
        if (policy is null)
        {
            return null;
        }

        var content = await documentStorage.ReadAsync(
            policy.ApplicationId,
            policy.PdfStoredFileName,
            cancellationToken);

        if (content is null)
        {
            throw new PolicyDocumentNotFoundException($"保單 {policy.PolicyNumber} 的 PDF 檔案不存在。");
        }

        return new PolicyDocumentContentDto(
            content,
            policy.PdfContentType,
            $"{policy.PolicyNumber}.pdf");
    }

    private async Task<string> GeneratePolicyNumberAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var prefix = $"POL-{businessDate:yyyyMM}-";
        var latest = await repository.GetLatestPolicyNumberAsync(prefix, cancellationToken);
        var next = 1;
        if (!string.IsNullOrWhiteSpace(latest) &&
            latest.Length > prefix.Length &&
            int.TryParse(latest[prefix.Length..], out var sequence))
        {
            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static string NormalizeTemplateVersion(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? DefaultTemplateVersion
            : value.Trim();

        if (normalized.Length > 50)
        {
            throw new PolicyValidationException("PolicyIssuance:TemplateVersion 不可超過 50 字元。");
        }

        return normalized;
    }

    private static DateOnly FromUtcToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            BusinessTimeZone));

    private static TimeZoneInfo ResolveBusinessTimeZone()
    {
        foreach (var id in new[] { "Asia/Taipei", "Taipei Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }

    private static string? BuildLifecycleDescription(PolicyLifecycle lifecycle)
    {
        if (string.IsNullOrWhiteSpace(lifecycle.LastTransition) && string.IsNullOrWhiteSpace(lifecycle.LastTransitionReason))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(lifecycle.LastTransitionReason))
        {
            return lifecycle.LastTransition;
        }

        if (string.IsNullOrWhiteSpace(lifecycle.LastTransition))
        {
            return lifecycle.LastTransitionReason;
        }

        return $"{lifecycle.LastTransition}：{lifecycle.LastTransitionReason}";
    }

    private static string SerializeIssuanceSnapshot(Policy policy) =>
        JsonSerializer.Serialize(
            new
            {
                policy.PolicyNumber,
                policy.ApplicationId,
                policy.PaymentId,
                policy.QuoteId,
                policy.CustomerId,
                policy.VehicleId,
                policy.Status,
                InsuranceStartDate = FromUtcToBusinessDate(policy.InsuranceStartDate),
                InsuranceEndDate = FromUtcToBusinessDate(policy.InsuranceEndDate),
                policy.TotalPremium,
                policy.PaymentFrequency,
                policy.PaymentMethod,
                policy.SalesAgent,
                policy.Channel,
                policy.RateVersion,
                policy.TemplateVersion,
                policy.PdfSizeBytes,
                policy.PdfSha256
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

    private static PolicyListItemDto MapListItem(Policy policy) => new(
        policy.Id,
        policy.PolicyNumber,
        policy.CustomerName,
        policy.InsuredName,
        policy.LicensePlate,
        policy.Status,
        FromUtcToBusinessDate(policy.InsuranceStartDate),
        FromUtcToBusinessDate(policy.InsuranceEndDate),
        policy.TotalPremium,
        policy.IssuedAtUtc);

    private static PolicyDetailsDto MapDetails(Policy policy) => new(
        policy.Id,
        policy.PolicyNumber,
        policy.ApplicationId,
        policy.Application.ApplicationNumber,
        policy.PaymentId,
        policy.Payment.PaymentNumber,
        policy.QuoteId,
        policy.Quote.QuoteNumber,
        policy.CustomerId,
        policy.CustomerName,
        policy.CustomerNationalId,
        policy.CustomerPhone,
        policy.CustomerEmail,
        policy.CustomerAddress,
        policy.VehicleId,
        policy.LicensePlate,
        policy.Vin,
        policy.MakeModelCode,
        policy.UsageType,
        policy.InsuredName,
        policy.InsuredNationalId,
        policy.Status,
        FromUtcToBusinessDate(policy.InsuranceStartDate),
        FromUtcToBusinessDate(policy.InsuranceEndDate),
        policy.TotalPremium,
        policy.PaymentFrequency,
        policy.PaymentMethod,
        policy.SalesAgent,
        policy.Channel,
        policy.RateVersion,
        policy.TemplateVersion,
        policy.PdfSizeBytes,
        policy.PdfSha256,
        policy.IssuedAtUtc,
        policy.CreatedAtUtc,
        policy.UpdatedAtUtc);

    private static PolicyDto Map(Policy policy) => new(
        policy.Id,
        policy.PolicyNumber,
        policy.ApplicationId,
        policy.Application.ApplicationNumber,
        policy.PaymentId,
        policy.Payment.PaymentNumber,
        policy.QuoteId,
        policy.Quote.QuoteNumber,
        policy.CustomerId,
        policy.CustomerName,
        policy.VehicleId,
        policy.LicensePlate,
        policy.Status,
        FromUtcToBusinessDate(policy.InsuranceStartDate),
        FromUtcToBusinessDate(policy.InsuranceEndDate),
        policy.TotalPremium,
        policy.PaymentFrequency,
        policy.PaymentMethod,
        policy.TemplateVersion,
        policy.PdfSizeBytes,
        policy.PdfSha256,
        policy.IssuedAtUtc,
        policy.CreatedAtUtc,
        policy.UpdatedAtUtc);
}

public sealed class PolicyValidationException(string message) : Exception(message);

public sealed class PolicyConflictException(string message) : Exception(message);

public sealed class PolicyNotFoundException(string message) : Exception(message);

public sealed class PolicyDocumentNotFoundException(string message) : Exception(message);
