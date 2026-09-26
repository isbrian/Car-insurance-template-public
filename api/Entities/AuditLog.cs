namespace CarInsurance.Api.Entities;

public sealed class AuditLog
{
    public long Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ObjectType { get; set; } = string.Empty;
    public string ObjectId { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public string SourceIp { get; set; } = string.Empty;
}
