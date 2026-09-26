using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record PremiumCalculationRequestDto(
    [Range(1, int.MaxValue)] int RateVersionId,
    DateOnly DriverBirthDate,
    [Required] string DriverGender,
    [Range(1, 10)] int ClaimRecordLevel,
    [Range(0, int.MaxValue)] int ClaimCountLast3Years,
    [Required] string VehicleCategory,
    [Required] string MakeModelCode,
    DateOnly ManufactureDate,
    [Required] string UsageType,
    decimal? ThirdPartyBodilyInjuryPerPerson,
    decimal? ThirdPartyPropertyDamagePerAccident,
    decimal? ExcessLiabilityCoverage,
    string? OwnDamageType,
    decimal? Deductible,
    bool Theft,
    decimal? DriverAccidentCoverage,
    IReadOnlyList<string>? Riders,
    bool IsRenewal,
    bool IsMultiVehicle,
    bool IsOnline);

public sealed record PremiumBreakdownItemDto(
    string Section,
    string Code,
    string Label,
    string Operation,
    decimal Value,
    decimal Amount,
    string? Note = null);

public sealed record PremiumCalculationResponseDto(
    int RateVersionId,
    string SourceVersion,
    bool IsProvisional,
    string DisplayWarning,
    decimal CompulsoryPremium,
    decimal VoluntaryPremium,
    decimal TotalPremium,
    IReadOnlyList<PremiumBreakdownItemDto> Breakdown);
