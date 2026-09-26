namespace CarInsurance.Api.Dtos;

public sealed record RenewalListItemDto(
    int PolicyId,
    string PolicyNumber,
    string CustomerName,
    string LicensePlate,
    DateOnly InsuranceStartDate,
    DateOnly InsuranceEndDate,
    int DaysUntilExpiry,
    decimal CurrentPremium,
    string LifecycleStatus,
    int? ExistingRenewalQuoteId);
