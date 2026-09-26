namespace CarInsurance.Api.Entities;

public class InsuranceProduct
{
    public int Id { get; set; }

    public int InsuranceTypeId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public InsuranceType InsuranceType { get; set; } = null!;

    public ICollection<RateVersion> RateVersions { get; set; } = new List<RateVersion>();
}
