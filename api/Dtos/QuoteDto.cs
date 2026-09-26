using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record QuoteCreateDto(
    [Range(1, int.MaxValue)] int CustomerId,
    [Range(1, int.MaxValue)] int VehicleId,
    DateOnly InsuranceStartDate,
    [Required, MaxLength(100)] string SalesAgent,
    [Required, MaxLength(100)] string Channel,
    [Required] string Status,
    [Required] PremiumCalculationRequestDto Calculation);

public sealed record QuoteUpdateDto(
    [Range(1, int.MaxValue)] int CustomerId,
    [Range(1, int.MaxValue)] int VehicleId,
    DateOnly InsuranceStartDate,
    [Required, MaxLength(100)] string SalesAgent,
    [Required, MaxLength(100)] string Channel,
    [Required] string Status,
    [Required] PremiumCalculationRequestDto Calculation);

public sealed record QuoteDto(
    int Id,
    string QuoteNumber,
    int CustomerId,
    string CustomerName,
    int VehicleId,
    string LicensePlate,
    int RateVersionId,
    int? RenewalSourcePolicyId,
    string Status,
    DateOnly InsuranceStartDate,
    string SalesAgent,
    string Channel,
    decimal CompulsoryPremium,
    decimal VoluntaryPremium,
    decimal TotalPremium,
    bool IsProvisional,
    string DisplayWarning,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime ValidUntilUtc,
    PremiumCalculationRequestDto Calculation,
    IReadOnlyList<PremiumBreakdownItemDto> Breakdown);
