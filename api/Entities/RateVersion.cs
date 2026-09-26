namespace CarInsurance.Api.Entities;

public class RateVersion
{
    public int Id { get; set; }

    public int InsuranceProductId { get; set; }

    public string Version { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    public string Confidence { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public InsuranceProduct InsuranceProduct { get; set; } = null!;

    public RateTable? RateTable { get; set; }
}
