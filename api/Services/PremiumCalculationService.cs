using System.Text.Json;
using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public sealed class PremiumCalculationService(IRateTableService rateTableService) : IPremiumCalculationService
{
    public async Task<PremiumCalculationResponseDto> CalculateAsync(
        PremiumCalculationRequestDto request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);

        var rateTable = await rateTableService.GetByRateVersionIdAsync(request.RateVersionId, cancellationToken)
            ?? throw new PremiumCalculationNotFoundException($"RateVersionId={request.RateVersionId} の料金テーブルが見つかりません。");

        var root = rateTable.Data;
        var breakdown = new List<PremiumBreakdownItemDto>();

        var compulsoryPremium = CalculateCompulsory(root, request, breakdown);
        var voluntaryPremium = CalculateVoluntary(root, request, breakdown);
        var totalPremium = RoundCurrency(compulsoryPremium + voluntaryPremium);

        return new PremiumCalculationResponseDto(
            rateTable.RateVersionId,
            rateTable.SourceVersion,
            rateTable.IsProvisional,
            rateTable.DisplayWarning,
            compulsoryPremium,
            voluntaryPremium,
            totalPremium,
            breakdown);
    }

    private static decimal CalculateCompulsory(
        JsonElement root,
        PremiumCalculationRequestDto request,
        List<PremiumBreakdownItemDto> breakdown)
    {
        var compulsory = GetRequiredObject(root, "compulsory");
        var basePremiums = GetRequiredObject(compulsory, "basePureNetPremium");
        var basePremiumKey = NormalizeCompulsoryVehicleCategory(request.VehicleCategory);
        var basePremium = GetRequiredDecimal(basePremiums, basePremiumKey);

        var gender = NormalizeGender(request.DriverGender);
        var age = CalculateAge(request.DriverBirthDate, DateOnly.FromDateTime(DateTime.UtcNow));
        var ageBucket = GetAgeBucket(age);
        var ageGenderFactors = GetRequiredObject(compulsory, "ageGenderFactors");
        var genderFactors = GetRequiredObject(ageGenderFactors, gender);
        var ageFactorEntry = GetRequiredObject(genderFactors, ageBucket);
        var ageGenderFactor = GetRequiredDecimal(ageFactorEntry, "value");

        var recordLevelFactors = GetRequiredObject(compulsory, "recordLevelFactors");
        var recordEntry = GetRequiredObject(recordLevelFactors, request.ClaimRecordLevel.ToString());
        var recordLevelFactor = GetRequiredDecimal(recordEntry, "value");

        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "basePureNetPremium",
            "基本純保険料",
            "base",
            basePremium,
            basePremium,
            basePremiumKey));

        var formula = GetRequiredObject(compulsory, "formula");
        var personalVehicles = GetRequiredArray(formula, "appliesPersonalFormulaTo")
            .EnumerateArray()
            .Select(x => x.GetString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.Ordinal);

        decimal adjustedPurePremium;
        if (personalVehicles.Contains(request.VehicleCategory.Trim()))
        {
            adjustedPurePremium = basePremium * (ageGenderFactor + recordLevelFactor - 1m);
            breakdown.Add(new PremiumBreakdownItemDto(
                "compulsory",
                "ageGenderFactor",
                "年齢・性別係数",
                "multiply",
                ageGenderFactor,
                adjustedPurePremium,
                $"age={age}, bucket={ageBucket}"));
        }
        else
        {
            adjustedPurePremium = basePremium * recordLevelFactor;
        }

        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "recordLevelFactor",
            "事故等級係数",
            "multiply",
            recordLevelFactor,
            adjustedPurePremium,
            $"level={request.ClaimRecordLevel}"));

        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "adjustedPureNetPremium",
            "調整後純保険料",
            "subtotal",
            1m,
            RoundCurrency(adjustedPurePremium)));

        var constants = GetRequiredObject(compulsory, "constants");
        var expenseFee = GetRequiredDecimal(constants, "expenseFee");
        var compensationFundRate = GetRequiredDecimal(constants, "compensationFundRate");
        var stabilizationFundRate = GetRequiredDecimal(constants, "stabilizationFundRate");
        var divisor = 1m - compensationFundRate - stabilizationFundRate;

        if (divisor <= 0m)
        {
            throw new PremiumCalculationConfigurationException("強制保険の基金率設定が不正です。");
        }

        var withExpense = adjustedPurePremium + expenseFee;
        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "expenseFee",
            "付加費用",
            "add",
            expenseFee,
            RoundCurrency(withExpense)));

        var premium = RoundCurrency(withExpense / divisor);
        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "fundDivisor",
            "基金控除後除数",
            "divide",
            divisor,
            premium));

        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "drunkDrivingSurcharge",
            "飲酒運転加算",
            "add",
            0m,
            premium,
            "現在の料金設定に具体的な加算額がないため未適用。"));

        breakdown.Add(new PremiumBreakdownItemDto(
            "compulsory",
            "compulsoryPremium",
            "強制保険料",
            "subtotal",
            1m,
            premium));

        return premium;
    }

    private static decimal CalculateVoluntary(
        JsonElement root,
        PremiumCalculationRequestDto request,
        List<PremiumBreakdownItemDto> breakdown)
    {
        var voluntary = GetRequiredObject(root, "voluntary");
        var factors = GetRequiredObject(voluntary, "factors");
        var total = 0m;

        if (request.ThirdPartyBodilyInjuryPerPerson.HasValue || request.ThirdPartyPropertyDamagePerAccident.HasValue)
        {
            if (!request.ThirdPartyBodilyInjuryPerPerson.HasValue || !request.ThirdPartyPropertyDamagePerAccident.HasValue)
            {
                throw new PremiumCalculationValidationException("第三者責任保険は対人・対物保額を両方指定してください。");
            }

            var plan = FindMatchingPlan(
                GetRequiredArray(voluntary, "thirdPartyLiability"),
                ("bodilyInjuryPerPerson", request.ThirdPartyBodilyInjuryPerPerson.Value),
                ("propertyDamagePerAccident", request.ThirdPartyPropertyDamagePerAccident.Value));

            var basePremium = GetRequiredDecimal(plan, "premium");
            var calculated = ApplyVoluntaryFactors(
                basePremium,
                factors,
                request,
                breakdown,
                "thirdPartyLiability",
                "第三者責任保険");
            total += calculated;
        }

        if (request.ExcessLiabilityCoverage.HasValue)
        {
            var plan = FindMatchingPlan(
                GetRequiredArray(voluntary, "excessLiability"),
                ("coverage", request.ExcessLiabilityCoverage.Value));
            var basePremium = GetRequiredDecimal(plan, "premium");
            total += ApplyVoluntaryFactors(
                basePremium,
                factors,
                request,
                breakdown,
                "excessLiability",
                "超額責任保険");
        }

        decimal? replacementValue = null;
        if (!string.IsNullOrWhiteSpace(request.OwnDamageType) || request.Theft)
        {
            replacementValue = GetReplacementValue(voluntary, request, breakdown);
        }

        decimal ownDamagePremium = 0m;
        if (!string.IsNullOrWhiteSpace(request.OwnDamageType))
        {
            var ownDamage = GetRequiredObject(voluntary, "ownDamage");
            var ownDamageRate = GetRequiredDecimal(ownDamage, request.OwnDamageType.Trim());
            var deductibleFactors = GetRequiredObject(ownDamage, "deductibleFactors");
            var deductible = request.Deductible ?? 0m;
            var deductibleFactor = GetRequiredDecimal(deductibleFactors, DecimalKey(deductible));

            var beforeDeductible = replacementValue!.Value * ownDamageRate;
            breakdown.Add(new PremiumBreakdownItemDto(
                "voluntary",
                "ownDamage:rate",
                "車体保険料率",
                "multiply",
                ownDamageRate,
                RoundCurrency(beforeDeductible),
                request.OwnDamageType.Trim()));

            var basePremium = beforeDeductible * deductibleFactor;
            breakdown.Add(new PremiumBreakdownItemDto(
                "voluntary",
                "ownDamage:deductibleFactor",
                "免責金額係数",
                "discount",
                deductibleFactor,
                RoundCurrency(basePremium),
                $"deductible={deductible:0}"));

            ownDamagePremium = ApplyVoluntaryFactors(
                basePremium,
                factors,
                request,
                breakdown,
                "ownDamage",
                $"車両保険 {request.OwnDamageType.Trim()}");
            total += ownDamagePremium;
        }

        if (request.Theft)
        {
            var theft = GetRequiredObject(voluntary, "theft");
            var theftRate = GetRequiredDecimal(theft, "rate");
            var basePremium = replacementValue!.Value * theftRate;
            total += ApplyVoluntaryFactors(
                basePremium,
                factors,
                request,
                breakdown,
                "theft",
                "盗難損失保険");
        }

        if (request.DriverAccidentCoverage.HasValue)
        {
            var plan = FindMatchingPlan(
                GetRequiredArray(voluntary, "driverAccident"),
                ("coverage", request.DriverAccidentCoverage.Value));
            var premium = GetRequiredDecimal(plan, "premium");
            total += premium;
            breakdown.Add(new PremiumBreakdownItemDto(
                "voluntary",
                "driverAccident",
                "運転者傷害保険",
                "base",
                premium,
                RoundCurrency(premium),
                $"coverage={request.DriverAccidentCoverage.Value:0}"));
        }

        if (request.Riders is { Count: > 0 })
        {
            var ridersConfig = GetRequiredObject(voluntary, "riders");
            foreach (var riderName in request.Riders.Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(riderName))
                {
                    continue;
                }

                var rider = GetRequiredObject(ridersConfig, riderName.Trim());
                var type = GetRequiredString(rider, "type");
                var value = GetRequiredDecimal(rider, "value");
                decimal riderPremium;

                if (string.Equals(type, "flat", StringComparison.Ordinal))
                {
                    riderPremium = value;
                }
                else if (string.Equals(type, "percentOfOwnDamage", StringComparison.Ordinal))
                {
                    if (ownDamagePremium <= 0m)
                    {
                        throw new PremiumCalculationValidationException($"特約「{riderName}」は車両保険の選択が必要です。");
                    }

                    riderPremium = ownDamagePremium * value;
                }
                else
                {
                    throw new PremiumCalculationConfigurationException($"特約「{riderName}」の type={type} は未対応です。");
                }

                riderPremium = RoundCurrency(riderPremium);
                total += riderPremium;
                breakdown.Add(new PremiumBreakdownItemDto(
                    "voluntary",
                    $"rider:{riderName}",
                    riderName,
                    "add",
                    value,
                    riderPremium,
                    type));
            }
        }

        total = RoundCurrency(total);
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            "voluntaryPremium",
            "任意保険料",
            "subtotal",
            1m,
            total));

        return total;
    }

    private static decimal ApplyVoluntaryFactors(
        decimal basePremium,
        JsonElement factors,
        PremiumCalculationRequestDto request,
        List<PremiumBreakdownItemDto> breakdown,
        string code,
        string label)
    {
        var amount = basePremium;
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            $"{code}:base",
            $"{label} 基本保険料",
            "base",
            basePremium,
            RoundCurrency(amount)));

        var claimRecord = GetRequiredObject(factors, "claimRecord");
        var claimKey = Math.Min(request.ClaimCountLast3Years, 3).ToString();
        var claimFactor = GetRequiredDecimal(claimRecord, claimKey);
        amount *= claimFactor;
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            $"{code}:claimRecord",
            "事故歴係数",
            "multiply",
            claimFactor,
            RoundCurrency(amount),
            $"claims={request.ClaimCountLast3Years}"));

        if (request.IsRenewal)
        {
            amount = ApplyOptionalFactor(amount, factors, "renewalDiscount", "更新割引", code, breakdown);
        }

        if (request.IsMultiVehicle)
        {
            amount = ApplyOptionalFactor(amount, factors, "multiVehicleDiscount", "複数台割引", code, breakdown);
        }

        if (request.IsOnline)
        {
            amount = ApplyOptionalFactor(amount, factors, "onlineDiscount", "オンライン割引", code, breakdown);
        }

        amount = RoundCurrency(amount);
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            $"{code}:subtotal",
            label,
            "subtotal",
            1m,
            amount));

        return amount;
    }

    private static decimal ApplyOptionalFactor(
        decimal amount,
        JsonElement factors,
        string factorName,
        string label,
        string code,
        List<PremiumBreakdownItemDto> breakdown)
    {
        var factor = GetRequiredDecimal(factors, factorName);
        amount *= factor;
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            $"{code}:{factorName}",
            label,
            "discount",
            factor,
            RoundCurrency(amount)));
        return amount;
    }

    private static decimal GetReplacementValue(
        JsonElement voluntary,
        PremiumCalculationRequestDto request,
        List<PremiumBreakdownItemDto> breakdown)
    {
        if (!voluntary.TryGetProperty("vehicleModels", out var vehicleModels) || vehicleModels.ValueKind != JsonValueKind.Object)
        {
            throw new PremiumCalculationValidationException("車両型式料金が未設定のため、車両保険または盗難保険を試算できません。");
        }

        var modelCode = request.MakeModelCode.Trim();
        if (!vehicleModels.TryGetProperty(modelCode, out var model) || model.ValueKind != JsonValueKind.Object)
        {
            throw new PremiumCalculationValidationException($"車両型式「{request.MakeModelCode}」の料金設定がありません。");
        }

        var modelBaseValue = GetRequiredDecimal(model, "baseValue");
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            "vehicleModelBaseValue",
            "車両型式基準価格",
            "base",
            modelBaseValue,
            RoundCurrency(modelBaseValue),
            modelCode));

        var years = CalculateAge(request.ManufactureDate, DateOnly.FromDateTime(DateTime.UtcNow));
        var depreciationFactors = GetRequiredObject(voluntary, "depreciationFactors");
        var depreciationFactor = GetDepreciationFactor(depreciationFactors, years);
        var replacementValue = modelBaseValue * depreciationFactor;

        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            "depreciationFactor",
            "車両減価係数",
            "multiply",
            depreciationFactor,
            RoundCurrency(replacementValue),
            $"vehicleAgeYears={years}"));
        breakdown.Add(new PremiumBreakdownItemDto(
            "voluntary",
            "replacementValue",
            "重置価格",
            "subtotal",
            1m,
            RoundCurrency(replacementValue)));

        return replacementValue;
    }

    private static decimal GetDepreciationFactor(JsonElement factors, int years)
    {
        var matchedYear = factors.EnumerateObject()
            .Select(property => int.TryParse(property.Name, out var year)
                ? new { Year = year, Property = property }
                : null)
            .Where(item => item is not null && item.Year <= years)
            .OrderByDescending(item => item!.Year)
            .FirstOrDefault();

        if (matchedYear is null)
        {
            throw new PremiumCalculationConfigurationException($"使用年数 {years} 年に適用できる減価係数がありません。");
        }

        return ReadDecimal(matchedYear.Property.Value, $"depreciationFactors.{matchedYear.Year}");
    }

    private static JsonElement FindMatchingPlan(
        JsonElement array,
        params (string Property, decimal Value)[] conditions)
    {
        foreach (var item in array.EnumerateArray())
        {
            var matches = conditions.All(condition =>
                item.TryGetProperty(condition.Property, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetDecimal(out var value) &&
                value == condition.Value);

            if (matches)
            {
                return item;
            }
        }

        var detail = string.Join(", ", conditions.Select(x => $"{x.Property}={x.Value:0.##}"));
        throw new PremiumCalculationValidationException($"料金表に一致するプランがありません: {detail}");
    }

    private static void ValidateRequest(PremiumCalculationRequestDto request)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (request.DriverBirthDate > today)
        {
            throw new PremiumCalculationValidationException("運転者の生年月日は未来日を指定できません。");
        }

        if (request.ManufactureDate > today)
        {
            throw new PremiumCalculationValidationException("製造年月日は未来日を指定できません。");
        }

        _ = NormalizeGender(request.DriverGender);

        if (string.IsNullOrWhiteSpace(request.VehicleCategory))
        {
            throw new PremiumCalculationValidationException("vehicleCategory は必須です。");
        }

        if (string.IsNullOrWhiteSpace(request.MakeModelCode))
        {
            throw new PremiumCalculationValidationException("makeModelCode は必須です。");
        }

        if (string.IsNullOrWhiteSpace(request.UsageType))
        {
            throw new PremiumCalculationValidationException("usageType は必須です。");
        }
    }

    private static string NormalizeGender(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "male" => "male",
            "female" => "female",
            _ => throw new PremiumCalculationValidationException("driverGender は male または female を指定してください。")
        };
    }

    private static string NormalizeCompulsoryVehicleCategory(string value)
    {
        var normalized = value.Trim();
        return normalized.EndsWith("(自然人)", StringComparison.Ordinal)
            ? normalized[..^"(自然人)".Length]
            : normalized;
    }

    private static int CalculateAge(DateOnly date, DateOnly today)
    {
        var years = today.Year - date.Year;
        if (date > today.AddYears(-years))
        {
            years--;
        }

        return Math.Max(years, 0);
    }

    private static string GetAgeBucket(int age) => age switch
    {
        < 20 => "under20",
        < 25 => "20to25",
        < 30 => "25to30",
        < 60 => "30to60",
        < 70 => "60to70",
        _ => "over70"
    };

    private static JsonElement GetRequiredObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
        {
            throw new PremiumCalculationConfigurationException($"料金設定に object '{propertyName}' がありません。");
        }

        return property;
    }

    private static JsonElement GetRequiredArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            throw new PremiumCalculationConfigurationException($"料金設定に array '{propertyName}' がありません。");
        }

        return property;
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new PremiumCalculationConfigurationException($"料金設定に string '{propertyName}' がありません。");
        }

        return property.GetString()!;
    }

    private static decimal GetRequiredDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            throw new PremiumCalculationConfigurationException($"料金設定に数値 '{propertyName}' がありません。");
        }

        return ReadDecimal(property, propertyName);
    }

    private static decimal ReadDecimal(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var value))
        {
            return value;
        }

        throw new PremiumCalculationConfigurationException($"料金設定 '{path}' が数値ではありません。");
    }

    private static string DecimalKey(decimal value) => decimal.Truncate(value) == value
        ? value.ToString("0")
        : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static decimal RoundCurrency(decimal value) => Math.Round(value, 0, MidpointRounding.AwayFromZero);
}

public sealed class PremiumCalculationValidationException(string message) : Exception(message);

public sealed class PremiumCalculationNotFoundException(string message) : Exception(message);

public sealed class PremiumCalculationConfigurationException(string message) : Exception(message);
