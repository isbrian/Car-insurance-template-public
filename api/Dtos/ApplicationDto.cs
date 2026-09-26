using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record ApplicationCreateDto(
    [Range(1, int.MaxValue)] int QuoteId);

public sealed record ApplicantStepDto(
    [Required, MaxLength(100)] string ApplicantName,
    [Required, MaxLength(50)] string RelationshipToOwner,
    [Required, MaxLength(20)] string PaymentFrequency);

public sealed record InsuredStepDto(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(20)] string NationalId,
    DateOnly BirthDate,
    [Required, MaxLength(50)] string RelationshipToOwner,
    [Required, MaxLength(30)] string Phone,
    [EmailAddress, MaxLength(254)] string? Email,
    [Required, MaxLength(300)] string Address);

public sealed record BeneficiaryInputDto(
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(20)] string NationalId,
    [Required, MaxLength(50)] string RelationshipToInsured,
    [Range(typeof(decimal), "0.01", "100.00")] decimal Percentage);

public sealed record BeneficiariesStepDto(
    [Required, MaxLength(20)] string Designation,
    [Required] IReadOnlyList<BeneficiaryInputDto> Beneficiaries);

public sealed record SignatureStatusStepDto(
    bool ApplicantSigned,
    bool InsuredSigned,
    bool AgentSigned);

public sealed record ApplicationSignatureCreateDto(
    [Required, MaxLength(20)] string SignerRole,
    [Required, MaxLength(700000)] string SignatureDataUrl);

public sealed record ApplicationSignatureDto(
    int Id,
    string SignerRole,
    string SignerName,
    string Method,
    string Sha256,
    DateTime SignedAtUtc,
    string? IpAddress,
    string? UserAgent);

public sealed record SignatureImageDto(
    byte[] Bytes,
    string Sha256);

public sealed record DocumentConfirmationStepDto(
    bool TermsAccepted,
    bool ElectronicPolicyAccepted);

public sealed record ApplicationDocumentReturnDto(
    [Required, MaxLength(500)] string Reason);

public sealed record ApplicationDocumentDto(
    int? Id,
    string DocumentType,
    string DisplayName,
    string Status,
    string? OriginalFileName,
    string? ContentType,
    long? SizeBytes,
    string? Sha256,
    string? ReturnReason,
    DateTime? UploadedAtUtc,
    DateTime? ReturnedAtUtc);

public sealed record ApplicationDocumentContentDto(
    byte[] Bytes,
    string ContentType,
    string OriginalFileName,
    string Sha256);

public sealed record ApplicationBeneficiaryDto(
    int Id,
    string Name,
    string NationalId,
    string RelationshipToInsured,
    decimal Percentage);

public sealed record InsuranceApplicationDto(
    int Id,
    string ApplicationNumber,
    int QuoteId,
    string QuoteNumber,
    string Status,
    int CurrentStep,
    int CustomerId,
    string CustomerName,
    int VehicleId,
    string LicensePlate,
    DateOnly InsuranceStartDate,
    string SalesAgent,
    string Channel,
    decimal TotalPremium,
    DateTime QuoteValidUntilUtc,
    string? ApplicantName,
    string? ApplicantRelationshipToOwner,
    string? PaymentFrequency,
    string? InsuredName,
    string? InsuredNationalId,
    DateOnly? InsuredBirthDate,
    string? InsuredRelationshipToOwner,
    string? InsuredPhone,
    string? InsuredEmail,
    string? InsuredAddress,
    string? BeneficiaryDesignation,
    IReadOnlyList<ApplicationBeneficiaryDto> Beneficiaries,
    IReadOnlyList<ApplicationSignatureDto> Signatures,
    IReadOnlyList<ApplicationDocumentDto> Documents,
    bool ApplicantSigned,
    bool InsuredSigned,
    bool AgentSigned,
    bool TermsAccepted,
    bool ElectronicPolicyAccepted,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? SubmittedAtUtc);
