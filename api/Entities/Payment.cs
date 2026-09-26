namespace CarInsurance.Api.Entities;

public class Payment
{
    public int Id { get; set; }

    public string PaymentNumber { get; set; } = string.Empty;

    public int ApplicationId { get; set; }

    public InsuranceApplication Application { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public string PaymentFrequency { get; set; } = string.Empty;

    public string PaymentMethod { get; set; } = string.Empty;

    public decimal AmountDue { get; set; }

    public int InstallmentCount { get; set; }

    public DateTime DueAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? ReferenceNumber { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
