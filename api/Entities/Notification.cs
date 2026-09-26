namespace CarInsurance.Api.Entities;

public sealed class Notification
{
    public int Id { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Recipient { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public string? LastError { get; set; }

    public string? ReferenceType { get; set; }

    public string? ReferenceId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }

    public DateTime? SentAtUtc { get; set; }

    public DateTime? ReadAtUtc { get; set; }
}
