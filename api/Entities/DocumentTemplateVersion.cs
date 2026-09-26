namespace CarInsurance.Api.Entities;

public sealed class DocumentTemplateVersion
{
    public int Id { get; set; }

    public int DocumentTemplateId { get; set; }

    public DocumentTemplate DocumentTemplate { get; set; } = null!;

    public string Version { get; set; } = string.Empty;

    public string? Content { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
