using System.Globalization;
using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public sealed class QuoteService(
    IQuoteRepository repository,
    IPremiumCalculationService premiumCalculationService,
    IPolicyLifecycleService policyLifecycleService,
    IAuditLogService auditLogService) : IQuoteService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeZoneInfo BusinessTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");

    public async Task<IReadOnlyList<QuoteDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<QuoteDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await repository.GetByIdAsync(id, cancellationToken);
        return quote is null ? null : Map(quote);
    }

    public async Task<QuoteDto> CreateAsync(
        QuoteCreateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var businessDate = GetBusinessDate(now);
        var status = NormalizeStatus(request.Status);
        ValidateInsuranceStartDate(request.InsuranceStartDate, businessDate);
        await ValidateRelationsAsync(request.CustomerId, request.VehicleId, cancellationToken);

        var calculation = await premiumCalculationService.CalculateAsync(request.Calculation, cancellationToken);
        var quoteNumber = await GenerateQuoteNumberAsync(businessDate, cancellationToken);
        var actor = RequireAuditActor(auditActor);

        var quote = new Quote
        {
            QuoteNumber = quoteNumber,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            RateVersionId = calculation.RateVersionId,
            Status = status,
            InsuranceStartDate = ToUtcStartOfBusinessDate(request.InsuranceStartDate),
            SalesAgent = request.SalesAgent.Trim(),
            Channel = request.Channel.Trim(),
            PremiumRequestJson = JsonSerializer.Serialize(request.Calculation, SnapshotJsonOptions),
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

        await repository.AddAsync(quote, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await policyLifecycleService.EnsureForQuoteAsync(quote.Id, cancellationToken);

        var saved = await repository.GetByIdAsync(quote.Id, cancellationToken)
            ?? throw new InvalidOperationException("建立報價後無法讀取資料。");
        await WriteAuditAsync(
            actor,
            "quote.created",
            saved.Id,
            null,
            SerializeQuoteSnapshot(saved),
            cancellationToken);
        return Map(saved);
    }

    public async Task<QuoteDto?> UpdateAsync(
        int id,
        QuoteUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken)
    {
        var quote = await repository.GetForUpdateAsync(id, cancellationToken);
        if (quote is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        if (now > quote.ValidUntilUtc)
        {
            throw new QuoteConflictException("報價已逾期，不可再更新。");
        }

        var businessDate = GetBusinessDate(now);
        var status = NormalizeStatus(request.Status);
        ValidateInsuranceStartDate(request.InsuranceStartDate, businessDate);
        await ValidateRelationsAsync(request.CustomerId, request.VehicleId, cancellationToken);

        var calculation = await premiumCalculationService.CalculateAsync(request.Calculation, cancellationToken);
        var insuranceStartDate = ToUtcStartOfBusinessDate(request.InsuranceStartDate);
        var salesAgent = request.SalesAgent.Trim();
        var channel = request.Channel.Trim();
        var premiumRequestJson = JsonSerializer.Serialize(request.Calculation, SnapshotJsonOptions);
        var premiumBreakdownJson = JsonSerializer.Serialize(calculation.Breakdown, SnapshotJsonOptions);
        var changes = GetQuoteChanges(
            quote,
            request.CustomerId,
            request.VehicleId,
            calculation.RateVersionId,
            status,
            insuranceStartDate,
            salesAgent,
            channel,
            premiumRequestJson,
            calculation.CompulsoryPremium,
            calculation.VoluntaryPremium,
            calculation.TotalPremium,
            calculation.IsProvisional,
            calculation.DisplayWarning);
        if (changes.Before.Count == 0)
        {
            return Map(quote);
        }

        var actor = RequireAuditActor(auditActor);
        quote.CustomerId = request.CustomerId;
        quote.VehicleId = request.VehicleId;
        quote.RateVersionId = calculation.RateVersionId;
        quote.Status = status;
        quote.InsuranceStartDate = insuranceStartDate;
        quote.SalesAgent = salesAgent;
        quote.Channel = channel;
        quote.PremiumRequestJson = premiumRequestJson;
        quote.PremiumBreakdownJson = premiumBreakdownJson;
        quote.CompulsoryPremium = calculation.CompulsoryPremium;
        quote.VoluntaryPremium = calculation.VoluntaryPremium;
        quote.TotalPremium = calculation.TotalPremium;
        quote.IsProvisional = calculation.IsProvisional;
        quote.DisplayWarning = calculation.DisplayWarning;
        quote.UpdatedAtUtc = now;

        await repository.SaveChangesAsync(cancellationToken);
        await WriteAuditAsync(
            actor,
            "quote.updated",
            id,
            JsonSerializer.Serialize(changes.Before, SnapshotJsonOptions),
            JsonSerializer.Serialize(changes.After, SnapshotJsonOptions),
            cancellationToken);

        var saved = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("更新報價後無法讀取資料。");
        return Map(saved);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var quote = await repository.GetForUpdateAsync(id, cancellationToken);
        if (quote is null)
        {
            return false;
        }

        await repository.RemoveAsync(quote, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task ValidateRelationsAsync(int customerId, int vehicleId, CancellationToken cancellationToken)
    {
        if (!await repository.CustomerExistsAsync(customerId, cancellationToken))
        {
            throw new QuoteNotFoundException($"找不到 CustomerId={customerId} 的客戶。");
        }

        var vehicleCustomerId = await repository.GetVehicleCustomerIdAsync(vehicleId, cancellationToken);
        if (!vehicleCustomerId.HasValue)
        {
            throw new QuoteNotFoundException($"找不到 VehicleId={vehicleId} 的車輛。");
        }

        if (vehicleCustomerId.Value != customerId)
        {
            throw new QuoteConflictException($"VehicleId={vehicleId} 不屬於 CustomerId={customerId}。");
        }
    }

    private async Task<string> GenerateQuoteNumberAsync(DateOnly businessDate, CancellationToken cancellationToken)
    {
        var prefix = $"QT-{businessDate:yyyyMM}-";
        var latest = await repository.GetLatestQuoteNumberAsync(prefix, cancellationToken);
        var next = 1;

        if (!string.IsNullOrWhiteSpace(latest))
        {
            var suffix = latest[prefix.Length..];
            if (!int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence))
            {
                throw new InvalidDataException($"既有報價編號格式錯誤：{latest}");
            }

            next = sequence + 1;
        }

        return $"{prefix}{next:0000}";
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = status.Trim().ToLowerInvariant();
        if (normalized is not ("draft" or "active"))
        {
            throw new QuoteValidationException("status 只接受 draft 或 active；expired 由系統依有效期限判定。");
        }

        return normalized;
    }

    private static void ValidateInsuranceStartDate(DateOnly insuranceStartDate, DateOnly businessDate)
    {
        if (insuranceStartDate < businessDate)
        {
            throw new QuoteValidationException("insuranceStartDate 不得早於今日。");
        }
    }

    private static DateOnly GetBusinessDate(DateTime utcNow) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, BusinessTimeZone));

    private static DateTime ToUtcStartOfBusinessDate(DateOnly businessDate)
    {
        var localMidnight = businessDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(localMidnight, BusinessTimeZone);
    }

    private static DateOnly FromUtcToBusinessDate(DateTime utcDateTime) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc), BusinessTimeZone));

    private static QuoteChanges GetQuoteChanges(
        Quote quote,
        int customerId,
        int vehicleId,
        int rateVersionId,
        string status,
        DateTime insuranceStartDate,
        string salesAgent,
        string channel,
        string premiumRequestJson,
        decimal compulsoryPremium,
        decimal voluntaryPremium,
        decimal totalPremium,
        bool isProvisional,
        string displayWarning)
    {
        var before = new Dictionary<string, object?>(StringComparer.Ordinal);
        var after = new Dictionary<string, object?>(StringComparer.Ordinal);

        AddChange(before, after, "customerId", quote.CustomerId, customerId);
        AddChange(before, after, "vehicleId", quote.VehicleId, vehicleId);
        AddChange(before, after, "rateVersionId", quote.RateVersionId, rateVersionId);
        AddChange(before, after, "status", quote.Status, status);
        AddChange(
            before,
            after,
            "insuranceStartDate",
            FromUtcToBusinessDate(quote.InsuranceStartDate),
            FromUtcToBusinessDate(insuranceStartDate));
        AddChange(before, after, "salesAgent", quote.SalesAgent, salesAgent);
        AddChange(before, after, "channel", quote.Channel, channel);
        if (!string.Equals(quote.PremiumRequestJson, premiumRequestJson, StringComparison.Ordinal))
        {
            before["calculation"] = JsonSerializer.Deserialize<JsonElement>(quote.PremiumRequestJson, SnapshotJsonOptions);
            after["calculation"] = JsonSerializer.Deserialize<JsonElement>(premiumRequestJson, SnapshotJsonOptions);
        }
        AddChange(before, after, "compulsoryPremium", quote.CompulsoryPremium, compulsoryPremium);
        AddChange(before, after, "voluntaryPremium", quote.VoluntaryPremium, voluntaryPremium);
        AddChange(before, after, "totalPremium", quote.TotalPremium, totalPremium);
        AddChange(before, after, "isProvisional", quote.IsProvisional, isProvisional);
        AddChange(before, after, "displayWarning", quote.DisplayWarning, displayWarning);

        return new QuoteChanges(before, after);
    }

    private static void AddChange<T>(
        IDictionary<string, object?> before,
        IDictionary<string, object?> after,
        string name,
        T oldValue,
        T newValue)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        before[name] = oldValue;
        after[name] = newValue;
    }

    private static string SerializeQuoteSnapshot(Quote quote) =>
        JsonSerializer.Serialize(
            new
            {
                quote.QuoteNumber,
                quote.CustomerId,
                quote.VehicleId,
                quote.RateVersionId,
                quote.Status,
                InsuranceStartDate = FromUtcToBusinessDate(quote.InsuranceStartDate),
                quote.SalesAgent,
                quote.Channel,
                Calculation = JsonSerializer.Deserialize<JsonElement>(quote.PremiumRequestJson, SnapshotJsonOptions),
                quote.CompulsoryPremium,
                quote.VoluntaryPremium,
                quote.TotalPremium,
                quote.IsProvisional,
                quote.DisplayWarning
            },
            SnapshotJsonOptions);

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
        int quoteId,
        string? beforeJson,
        string? afterJson,
        CancellationToken cancellationToken) =>
        auditLogService.WriteAsync(
            new AuditLogWriteRequest(
                actor.Actor,
                actor.Role,
                action,
                "Quote",
                quoteId.ToString(CultureInfo.InvariantCulture),
                beforeJson,
                afterJson,
                actor.SourceIp),
            cancellationToken);

    private static QuoteDto Map(Quote quote)
    {
        var calculation = JsonSerializer.Deserialize<PremiumCalculationRequestDto>(
            quote.PremiumRequestJson,
            SnapshotJsonOptions)
            ?? throw new InvalidDataException($"QuoteId={quote.Id} 的試算輸入快照無法解析。");

        var breakdown = JsonSerializer.Deserialize<List<PremiumBreakdownItemDto>>(
            quote.PremiumBreakdownJson,
            SnapshotJsonOptions)
            ?? throw new InvalidDataException($"QuoteId={quote.Id} 的保費拆解快照無法解析。");

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
    private sealed record QuoteChanges(
        Dictionary<string, object?> Before,
        Dictionary<string, object?> After);
}

public sealed class QuoteValidationException(string message) : Exception(message);

public sealed class QuoteNotFoundException(string message) : Exception(message);

public sealed class QuoteConflictException(string message) : Exception(message);
