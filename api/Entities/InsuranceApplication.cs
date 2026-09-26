namespace CarInsurance.Api.Entities;

public class InsuranceApplication
{
    public int Id { get; set; }

    public string ApplicationNumber { get; set; } = string.Empty;

    public int QuoteId { get; set; }

    public Quote Quote { get; set; } = null!;

    public string Status { get; set; } = string.Empty;

    public int CurrentStep { get; set; }

    public string? ApplicantName { get; set; }

    public string? ApplicantRelationshipToOwner { get; set; }

    public string? PaymentFrequency { get; set; }

    public string? InsuredName { get; set; }

    public string? InsuredNationalId { get; set; }

    public DateTime? InsuredBirthDate { get; set; }

    public string? InsuredRelationshipToOwner { get; set; }

    public string? InsuredPhone { get; set; }

    public string? InsuredEmail { get; set; }

    public string? InsuredAddress { get; set; }

    public string? BeneficiaryDesignation { get; set; }

    public bool ApplicantSigned { get; set; }

    public bool InsuredSigned { get; set; }

    public bool AgentSigned { get; set; }

    public bool TermsAccepted { get; set; }

    public bool ElectronicPolicyAccepted { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? SubmittedAtUtc { get; set; }

    public ICollection<ApplicationBeneficiary> Beneficiaries { get; set; } = new List<ApplicationBeneficiary>();

    public ICollection<ApplicationSignature> Signatures { get; set; } = new List<ApplicationSignature>();

    public ICollection<ApplicationDocument> Documents { get; set; } = new List<ApplicationDocument>();
}
