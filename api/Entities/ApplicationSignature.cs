namespace CarInsurance.Api.Entities;

public class ApplicationSignature
{
    public int Id { get; set; }

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public string SignerRole { get; set; } = string.Empty;

    public string SignerName { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string SignatureDataUrl { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public DateTime SignedAtUtc { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
