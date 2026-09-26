namespace CarInsurance.Api.Entities;

public class InsuranceType
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<InsuranceProduct> Products { get; set; } = new List<InsuranceProduct>();
}
