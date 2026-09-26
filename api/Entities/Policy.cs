namespace CarInsurance.Api.Entities;

public sealed class Policy
{
    public int Id { get; set; }

    public string PolicyNumber { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public int PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    public int QuoteId { get; set; }

    public Quote Quote { get; set; } = null!;

    public int CustomerId { get; set; }

    public int VehicleId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime InsuranceStartDate { get; set; }

    public DateTime InsuranceEndDate { get; set; }

    public decimal TotalPremium { get; set; }

    public string PaymentFrequency { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerNationalId { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public string? CustomerAddress { get; set; }

    public string LicensePlate { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public string MakeModelCode { get; set; } = string.Empty;

    public string UsageType { get; set; } = string.Empty;

    public string? InsuredName { get; set; }

    public string? InsuredNationalId { get; set; }

    public string SalesAgent { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string RateVersion { get; set; } = string.Empty;

    public string TemplateVersion { get; set; } = string.Empty;

    public string PdfStoredFileName { get; set; } = string.Empty;

    public string PdfContentType { get; set; } = string.Empty;

    public long PdfSizeBytes { get; set; }

    public string PdfSha256 { get; set; } = string.Empty;

    public DateTime IssuedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
