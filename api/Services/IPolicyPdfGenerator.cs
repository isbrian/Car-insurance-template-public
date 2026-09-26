namespace CarInsurance.Api.Services;

public sealed record PolicyPdfData(
    string PolicyNumber,
    string TemplateVersion,
    DateTime IssuedAtUtc,
    string ApplicationNumber,
    string QuoteNumber,
    string PaymentNumber,
    string CustomerName,
    string? InsuredName,
    string LicensePlate,
    string Vin,
    string MakeModelCode,
    string UsageType,
    DateOnly InsuranceStartDate,
    DateOnly InsuranceEndDate,
    decimal TotalPremium,
    string PaymentFrequency,
    string PaymentMethod,
    string SalesAgent,
    string Channel,
    string RateVersion);

public interface IPolicyPdfGenerator
{
    byte[] Generate(PolicyPdfData data);
}
