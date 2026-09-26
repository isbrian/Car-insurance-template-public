namespace CarInsurance.Api.Dtos;

public sealed record PolicyDto(
    int Id,
    string PolicyNumber,
    int ApplicationId,
    string ApplicationNumber,
    int PaymentId,
    string PaymentNumber,
    int QuoteId,
    string QuoteNumber,
    int CustomerId,
    string CustomerName,
    int VehicleId,
    string LicensePlate,
    string Status,
    DateOnly InsuranceStartDate,
    DateOnly InsuranceEndDate,
    decimal TotalPremium,
    string PaymentFrequency,
    string PaymentMethod,
    string TemplateVersion,
    long PdfSizeBytes,
    string PdfSha256,
    DateTime IssuedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PolicyDocumentContentDto(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record PolicyListItemDto(
    int Id,
    string PolicyNumber,
    string CustomerName,
    string? InsuredName,
    string LicensePlate,
    string Status,
    DateOnly InsuranceStartDate,
    DateOnly InsuranceEndDate,
    decimal TotalPremium,
    DateTime IssuedAtUtc);

public sealed record PolicyListResponse(
    IReadOnlyList<PolicyListItemDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record PolicyDetailsDto(
    int Id,
    string PolicyNumber,
    int ApplicationId,
    string ApplicationNumber,
    int PaymentId,
    string PaymentNumber,
    int QuoteId,
    string QuoteNumber,
    int CustomerId,
    string CustomerName,
    string CustomerNationalId,
    string CustomerPhone,
    string? CustomerEmail,
    string? CustomerAddress,
    int VehicleId,
    string LicensePlate,
    string Vin,
    string MakeModelCode,
    string UsageType,
    string? InsuredName,
    string? InsuredNationalId,
    string Status,
    DateOnly InsuranceStartDate,
    DateOnly InsuranceEndDate,
    decimal TotalPremium,
    string PaymentFrequency,
    string PaymentMethod,
    string SalesAgent,
    string Channel,
    string RateVersion,
    string TemplateVersion,
    long PdfSizeBytes,
    string PdfSha256,
    DateTime IssuedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PolicyTimelineEventDto(
    string Key,
    string Title,
    string Status,
    string? Description,
    DateTime OccurredAtUtc);

public sealed record PolicyTimelineDto(
    int PolicyId,
    string PolicyNumber,
    string? CurrentLifecycleStatus,
    IReadOnlyList<PolicyTimelineEventDto> Events);
