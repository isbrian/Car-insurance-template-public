namespace CarInsurance.Api.Entities;

public class ApplicationBeneficiary
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public string RelationshipToInsured { get; set; } = string.Empty;

    public decimal Percentage { get; set; }
}
