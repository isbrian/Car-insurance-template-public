using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IApplicationService
{
    Task<IReadOnlyList<InsuranceApplicationDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> GetByQuoteIdAsync(int quoteId, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto> CreateAsync(ApplicationCreateDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> UpdateApplicantAsync(int id, ApplicantStepDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> UpdateInsuredAsync(int id, InsuredStepDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> UpdateBeneficiariesAsync(int id, BeneficiariesStepDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> UpdateSignatureStatusAsync(int id, SignatureStatusStepDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> SignAsync(int id, ApplicationSignatureCreateDto request, string? ipAddress, string? userAgent, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> ClearSignatureAsync(int id, string signerRole, CancellationToken cancellationToken);
    Task<SignatureImageDto?> GetSignatureImageAsync(int id, string signerRole, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationDocumentDto>?> GetDocumentsAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationDocumentDto>?> UploadDocumentAsync(int id, string documentType, string originalFileName, string contentType, long length, Stream stream, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<ApplicationDocumentContentDto?> GetDocumentContentAsync(int id, string documentType, CancellationToken cancellationToken);
    Task<IReadOnlyList<ApplicationDocumentDto>?> ReturnDocumentAsync(int id, string documentType, ApplicationDocumentReturnDto request, AuditActorContext auditActor, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> UpdateDocumentConfirmationAsync(int id, DocumentConfirmationStepDto request, CancellationToken cancellationToken);
    Task<InsuranceApplicationDto?> SubmitAsync(
        int id,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
}
