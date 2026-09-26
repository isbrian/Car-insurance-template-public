namespace CarInsurance.Api.Entities;

public sealed class ApplicationDocument
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public string DocumentType { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ReturnReason { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public DateTime? ReturnedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
