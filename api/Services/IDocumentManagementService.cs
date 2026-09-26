using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IDocumentManagementService
{
    Task<CaseDocumentListResponse> GetCaseDocumentsAsync(
        string? keyword,
        string? status,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentTemplateSummaryDto>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<DocumentTemplateDetailDto?> GetTemplateAsync(int id, CancellationToken cancellationToken);
    Task<DocumentTemplateDetailDto> CreateTemplateAsync(DocumentTemplateCreateDto request, CancellationToken cancellationToken);
    Task<DocumentTemplateDetailDto?> AddVersionAsync(int id, DocumentTemplateVersionCreateDto request, CancellationToken cancellationToken);
    Task<DocumentTemplateDetailDto?> SetActiveVersionAsync(int id, DocumentTemplateActivateDto request, CancellationToken cancellationToken);
}

public sealed class DocumentManagementValidationException(string message) : Exception(message);
public sealed class DocumentManagementConflictException(string message) : Exception(message);
