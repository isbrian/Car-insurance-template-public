using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Data;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class RenewalService(
    IRenewalRepository repository,
    IPremiumCalculationService premiumCalculationService,
    IPolicyLifecycleService policyLifecycleService,
    CarInsuranceDbContext dbContext) : IRenewalService
{
    private const int RenewalWindowDays = 60;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

    public async Task<IReadOnlyList<RenewalListItemDto>> GetEligibleAsync(
        CancellationToken cancellationToken)
    {
        var businessDate = GetBusinessDate(DateTime.UtcNow);
        var startUtc = ToUtcStartOfBusinessDate(businessDate);
        var endUtc = ToUtcStartOfBusinessDate(businessDate.AddDays(RenewalWindowDays));
        var sources = await repository.GetEligibleAsync(startUtc, endUtc, cancellationToken);
        var result = new List<RenewalListItemDto>();

        foreach (var source in sources)
        {
            if (source.Lifecycle is null)
            {
                throw new RenewalDataIntegrityException(
                    $"PolicyId={source.Policy.Id} 找不到對應的保單生命週期。");
            }

            var lifecycleStatus = source.Lifecycle.Status;
            if (string.Equals(lifecycleStatus, PolicyLifecycleService.Statuses.Active, StringComparison.Ordinal))
            {
                var transitioned = await policyLifecycleService.TransitionByApplicationAsync(
                    source.Policy.ApplicationId,
                    PolicyLifecycleService.Statuses.RenewalPending,
                    "renewal_window_entered",
                    "保單已進入到期前 60 天續保窗口。",
                    cancellationToken)
                    ?? throw new RenewalDataIntegrityException(
                        $"PolicyId={source.Policy.Id} 的生命週期無法同步為待續保。");
                lifecycleStatus = transitioned.Status;
            }

            var hasRenewalQuote = source.ExistingRenewalQuoteId.HasValue;
            var visible = string.Equals(
                    lifecycleStatus,
                    PolicyLifecycleService.Statuses.RenewalPending,
                    StringComparison.Ordinal) ||
                (hasRenewalQuote && string.Equals(
                    lifecycleStatus,
                    PolicyLifecycleService.Statuses.Quotation,
                    StringComparison.Ordinal));

            if (!visible)
            {
                continue;
            }

            var endDate = FromUtcToBusinessDate(source.Policy.InsuranceEndDate);
            result.Add(new RenewalListItemDto(
                source.Policy.Id,
                source.Policy.PolicyNumber,
                source.Policy.CustomerName,
                source.Policy.LicensePlate,
                FromUtcToBusinessDate(source.Policy.InsuranceStartDate),
                endDate,
                endDate.DayNumber - businessDate.DayNumber,
                source.Policy.TotalPremium,
                lifecycleStatus,
                source.ExistingRenewalQuoteId));
        }

        return result;
    }

    public async Task<QuoteDto> CreateQuoteAsync(
        int policyId,
        CancellationToken cancellationToken)
    {
        var source = await repository.GetSourceByPolicyIdAsync(policyId, cancellationToken)
            ?? throw new RenewalNotFoundException($"找不到 PolicyId={policyId} 的保單。");

        var businessDate = GetBusinessDate(DateTime.UtcNow);
        var insuranceEndDate = FromUtcToBusinessDate(source.Policy.InsuranceEndDate);
        var daysUntilExpiry = insuranceEndDate.DayNumber - businessDate.DayNumber;
        if (daysUntilExpiry is < 0 or > RenewalWindowDays)
        {
            throw new RenewalConflictException(
                $"保單尚未進入到期前 {RenewalWindowDays} 天續保窗口。");
        }

        if (source.ExistingRenewalQuoteId.HasValue)
        {
            throw new RenewalConflictException(
                $"PolicyId={policyId} 已建立續保 QuoteId={source.ExistingRenewalQuoteId.Value}。");
        }

        if (source.Lifecycle is null)
        {
            throw new RenewalDataIntegrityException($"PolicyId={policyId} 找不到對應的保單生命週期。");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var lifecycleStatus = source.Lifecycle.Status;
        if (string.Equals(lifecycleStatus, PolicyLifecycleService.Statuses.Active, StringComparison.Ordinal))
        {
            var transitioned = await policyLifecycleService.TransitionByApplicationAsync(
                source.Policy.ApplicationId,
                PolicyLifecycleService.Statuses.RenewalPending,
                "renewal_window_entered",
                "保單已進入到期前 60 天續保窗口。",
                cancellationToken)
                ?? throw new RenewalDataIntegrityException(
                    $"PolicyId={policyId} 的生命週期無法同步為待續保。");
            lifecycleStatus = transitioned.Status;
        }

        if (!string.Equals(
                lifecycleStatus,
                PolicyLifecycleService.Statuses.RenewalPending,
                StringComparison.Ordinal))
        {
            throw new RenewalConflictException(
                $"目前生命週期為 {lifecycleStatus}，不可建立續保報價。");
        }

        PremiumCalculationRequestDto originalCalculation;
        try
        {
            originalCalculation = JsonSerializer.Deserialize<PremiumCalculationRequestDto>(
                source.Policy.Quote.PremiumRequestJson,
                SnapshotJsonOptions)
                ?? throw new JsonException("續保來源報價試算快照為空。");
        }
        catch (JsonException ex)
        {
            throw new RenewalDataIntegrityException(
                $"QuoteId={source.Policy.QuoteId} 的試算輸入快照無法解析。",
                ex);
        }

        var customerClaimRecordLevel = await repository.GetCustomerClaimRecordLevelAsync(
            source.Policy.CustomerId,
            cancellationToken)
            ?? throw new RenewalDataIntegrityException(
                $"PolicyId={policyId} 的 CustomerId={source.Policy.CustomerId} 不存在，無法取得賠款紀錄等級。");
        var claimWindowStartUtc = ToUtcStartOfBusinessDate(businessDate.AddYears(-3));
        var claimWindowEndUtc = ToUtcStartOfBusinessDate(businessDate);
        var claimCountLast3Years = await repository.CountPaidClaimsAsync(
            source.Policy.CustomerId,
            claimWindowStartUtc,
            claimWindowEndUtc,
            cancellationToken);

        var renewalCalculation = originalCalculation with
        {
            IsRenewal = true,
            ClaimRecordLevel = customerClaimRecordLevel,
            ClaimCountLast3Years = claimCountLast3Years
        };
        var calculation = await premiumCalculationService.CalculateAsync(
            renewalCalculation,
            cancellationToken);
        var now = DateTime.UtcNow;
        var quoteNumber = await GenerateQuoteNumberAsync(businessDate, cancellationToken);
        var renewalStartDate = insuranceEndDate.AddDays(1);

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            CustomerId = source.Policy.CustomerId,
            VehicleId = source.Policy.VehicleId,
            RateVersionId = calculation.RateVersionId,
            RenewalSourcePolicyId = source.Policy.Id,
            Status = "draft",
            InsuranceStartDate = ToUtcStartOfBusinessDate(renewalStartDate),
            SalesAgent = source.Policy.Quote.SalesAgent,
            Channel = source.Policy.Quote.Channel,
            PremiumRequestJson = JsonSerializer.Serialize(renewalCalculation, SnapshotJsonOptions),
            PremiumBreakdownJson = JsonSerializer.Serialize(calculation.Breakdown, SnapshotJsonOptions),
            CompulsoryPremium = calculation.CompulsoryPremium,
            VoluntaryPremium = calculation.VoluntaryPremium,
            TotalPremium = calculation.TotalPremium,
            IsProvisional = calculation.IsProvisional,
            DisplayWarning = calculation.DisplayWarning,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ValidUntilUtc = now.AddDays(30)
        };

        await repository.AddQuoteAsync(quote, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        _ = await policyLifecycleService.EnsureForQuoteAsync(quote.Id, cancellationToken)
            ?? throw new RenewalDataIntegrityException(
                $"建立 QuoteId={quote.Id} 後無法建立生命週期。");

        _ = await policyLifecycleService.TransitionByApplicationAsync(
            source.Policy.ApplicationId,
            PolicyLifecycleService.Statuses.Quotation,
            "renewal_quote_created",
            $"已建立續保報價 {quote.QuoteNumber}。",
            cancellationToken)
            ?? throw new RenewalDataIntegrityException(
                $"PolicyId={policyId} 建立續保報價後無法更新來源生命週期。");

        await transaction.CommitAsync(cancellationToken);

        var saved = await repository.GetQuoteByIdAsync(quote.Id, cancellationToken)
            ?? throw new RenewalDataIntegrityException("建立續保報價後無法重新讀取資料。");
        return MapQuote(saved);
    }

    private async Task<string> GenerateQuoteNumberAsync(
        DateOnly businessDate,
        CancellationToken cancellationToken)
    {
        var prefix = $"QT-{businessDate:yyyyMM}-";
        var latest = await repository.GetLatestQuoteNumberAsync(prefix, cancellationToken);
        var next = 1;

        if (!string.IsNullOrWhiteSpace(latest))
        {
            var suffix = latest[prefix.Length..];
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            {
                throw new RenewalDataIntegrityException($"既有報價編號格式錯誤：{latest}");
            }

            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static QuoteDto MapQuote(Quote quote)
    {
        var calculation = JsonSerializer.Deserialize<PremiumCalculationRequestDto>(
            quote.PremiumRequestJson,
            SnapshotJsonOptions)
            ?? throw new RenewalDataIntegrityException($"QuoteId={quote.Id} 的試算輸入快照無法解析。");
        var breakdown = JsonSerializer.Deserialize<List<PremiumBreakdownItemDto>>(
            quote.PremiumBreakdownJson,
            SnapshotJsonOptions)
            ?? throw new RenewalDataIntegrityException($"QuoteId={quote.Id} 的保費拆解快照無法解析。");
        var status = DateTime.UtcNow > quote.ValidUntilUtc ? "expired" : quote.Status;

        return new QuoteDto(
            quote.Id,
            quote.QuoteNumber,
            quote.CustomerId,
            quote.Customer.Name,
            quote.VehicleId,
            quote.Vehicle.LicensePlate,
            quote.RateVersionId,
            quote.RenewalSourcePolicyId,
            status,
            FromUtcToBusinessDate(quote.InsuranceStartDate),
            quote.SalesAgent,
            quote.Channel,
            quote.CompulsoryPremium,
            quote.VoluntaryPremium,
            quote.TotalPremium,
            quote.IsProvisional,
            quote.DisplayWarning,
            quote.CreatedAtUtc,
            quote.UpdatedAtUtc,
            quote.ValidUntilUtc,
            calculation,
            breakdown);
    }

    private static DateOnly GetBusinessDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, BusinessTimeZone));

    private static DateTime ToUtcStartOfBusinessDate(DateOnly businessDate)
    {
        var localMidnight = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessTimeZone);
    }

    private static DateOnly FromUtcToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc),
            BusinessTimeZone));
}

public sealed class RenewalNotFoundException(string message) : Exception(message);

public sealed class RenewalConflictException(string message) : Exception(message);

public sealed class RenewalDataIntegrityException : Exception
{
    public RenewalDataIntegrityException(string message) : base(message)
    {
    }

    public RenewalDataIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
