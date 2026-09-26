using CarInsurance.Api.Entities;

namespace CarInsurance.Api.Repositories;

public sealed record CaseDocumentQueryResult(
    IReadOnlyList<ApplicationDocument> Items,
    int Total);

public interface IDocumentManagementRepository
{
    Task<CaseDocumentQueryResult> GetCaseDocumentsAsync(
        string? keyword,
        string? status,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentTemplate>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<DocumentTemplate?> GetTemplateAsync(int id, CancellationToken cancellationToken);
    Task<DocumentTemplate?> GetTemplateForUpdateAsync(int id, CancellationToken cancellationToken);
    Task<bool> TemplateCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> TemplateVersionExistsAsync(int templateId, string version, CancellationToken cancellationToken);
    Task AddTemplateAsync(DocumentTemplate template, CancellationToken cancellationToken);
    Task AddVersionAsync(DocumentTemplateVersion version, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
