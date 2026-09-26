using CarInsurance.Api.Data;
using CarInsurance.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Repositories;

public sealed class DocumentManagementRepository(CarInsuranceDbContext dbContext) : IDocumentManagementRepository
{
    public async Task<CaseDocumentQueryResult> GetCaseDocumentsAsync(
        string? keyword,
        string? status,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.ApplicationDocuments
            .AsNoTracking()
            .Include(x => x.Application)
                .ThenInclude(x => x.Quote)
                    .ThenInclude(x => x.Customer)
            .Include(x => x.Application)
                .ThenInclude(x => x.Quote)
                    .ThenInclude(x => x.Vehicle)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.Trim();
            query = query.Where(x =>
                x.Application.ApplicationNumber.Contains(normalized) ||
                x.Application.Quote.Customer.Name.Contains(normalized) ||
                x.Application.Quote.Vehicle.LicensePlate.Contains(normalized) ||
                x.OriginalFileName.Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(documentType))
        {
            query = query.Where(x => x.DocumentType == documentType);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new CaseDocumentQueryResult(items, total);
    }

    public async Task<IReadOnlyList<DocumentTemplate>> GetTemplatesAsync(CancellationToken cancellationToken) =>
        await dbContext.DocumentTemplates
            .AsNoTracking()
            .Include(x => x.ActiveVersion)
            .Include(x => x.Versions)
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);

    public Task<DocumentTemplate?> GetTemplateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplates
            .AsNoTracking()
            .Include(x => x.ActiveVersion)
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<DocumentTemplate?> GetTemplateForUpdateAsync(int id, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplates
            .Include(x => x.ActiveVersion)
            .Include(x => x.Versions)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<bool> TemplateCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplates.AnyAsync(x => x.Code == code, cancellationToken);

    public Task<bool> TemplateVersionExistsAsync(int templateId, string version, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplateVersions.AnyAsync(
            x => x.DocumentTemplateId == templateId && x.Version == version,
            cancellationToken);

    public Task AddTemplateAsync(DocumentTemplate template, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplates.AddAsync(template, cancellationToken).AsTask();

    public Task AddVersionAsync(DocumentTemplateVersion version, CancellationToken cancellationToken) =>
        dbContext.DocumentTemplateVersions.AddAsync(version, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
