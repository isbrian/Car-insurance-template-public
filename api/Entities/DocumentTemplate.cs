namespace CarInsurance.Api.Entities;

public sealed class DocumentTemplate
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int? ActiveVersionId { get; set; }

    public DocumentTemplateVersion? ActiveVersion { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<DocumentTemplateVersion> Versions { get; set; } = new List<DocumentTemplateVersion>();
}
