namespace CarInsurance.Api.Entities;

public class Quote
{
    public int Id { get; set; }

    public string QuoteNumber { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public int VehicleId { get; set; }

    public Vehicle Vehicle { get; set; } = null!;

    public int RateVersionId { get; set; }

    public RateVersion RateVersion { get; set; } = null!;

    public int? RenewalSourcePolicyId { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime InsuranceStartDate { get; set; }

    public string SalesAgent { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string PremiumRequestJson { get; set; } = string.Empty;

    public string PremiumBreakdownJson { get; set; } = string.Empty;

    public decimal CompulsoryPremium { get; set; }

    public decimal VoluntaryPremium { get; set; }

    public decimal TotalPremium { get; set; }

    public bool IsProvisional { get; set; }

    public string DisplayWarning { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime ValidUntilUtc { get; set; }
}
