namespace CarInsurance.Api.Entities;

public class RateTable
{
    public int Id { get; set; }

    public int RateVersionId { get; set; }

    public string SourceVersion { get; set; } = string.Empty;

    public bool IsProvisional { get; set; }

    public string DisplayWarning { get; set; } = string.Empty;

    public string JsonData { get; set; } = string.Empty;

    public DateTime ImportedAtUtc { get; set; }

    public RateVersion RateVersion { get; set; } = null!;
}
