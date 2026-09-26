using System.Security.Cryptography;
using System.Text;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Entities;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class DocumentManagementService(IDocumentManagementRepository repository) : IDocumentManagementService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "uploaded",
        "returned"
    };

    public async Task<CaseDocumentListResponse> GetCaseDocumentsAsync(
        string? keyword,
        string? status,
        string? documentType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (page < 1) throw new DocumentManagementValidationException("page 必須大於等於 1。");
        if (pageSize is < 1 or > 100) throw new DocumentManagementValidationException("pageSize 必須介於 1 到 100。");

        var normalizedStatus = NormalizeOptional(status)?.ToLowerInvariant();
        if (normalizedStatus is not null && !AllowedStatuses.Contains(normalizedStatus))
        {
            throw new DocumentManagementValidationException("status 只接受 uploaded 或 returned。");
        }

        var normalizedDocumentType = NormalizeOptional(documentType)?.ToLowerInvariant();

        var result = await repository.GetCaseDocumentsAsync(
            NormalizeOptional(keyword),
            normalizedStatus,
            normalizedDocumentType,
            page,
            pageSize,
            cancellationToken);

        return new CaseDocumentListResponse(
            result.Items.Select(MapCaseDocument).ToList(),
            page,
            pageSize,
            result.Total);
    }

    public async Task<IReadOnlyList<DocumentTemplateSummaryDto>> GetTemplatesAsync(CancellationToken cancellationToken) =>
        (await repository.GetTemplatesAsync(cancellationToken))
            .Select(MapTemplateSummary)
            .ToList();

    public async Task<DocumentTemplateDetailDto?> GetTemplateAsync(int id, CancellationToken cancellationToken)
    {
        var template = await repository.GetTemplateAsync(id, cancellationToken);
        return template is null ? null : MapTemplateDetail(template);
    }

    public async Task<DocumentTemplateDetailDto> CreateTemplateAsync(
        DocumentTemplateCreateDto request,
        CancellationToken cancellationToken)
    {
        var code = Required(request.Code, 50, "code").ToLowerInvariant();
        var name = Required(request.Name, 150, "name");
        var description = Optional(request.Description, 500, "description");

        if (await repository.TemplateCodeExistsAsync(code, cancellationToken))
        {
            throw new DocumentManagementConflictException($"範本 code={code} 已存在。");
        }

        var now = DateTime.UtcNow;
        var template = new DocumentTemplate
        {
            Code = code,
            Name = name,
            Description = description,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.AddTemplateAsync(template, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return MapTemplateDetail(template);
    }

    public async Task<DocumentTemplateDetailDto?> AddVersionAsync(
        int id,
        DocumentTemplateVersionCreateDto request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetTemplateForUpdateAsync(id, cancellationToken);
        if (template is null) return null;

        var versionValue = Required(request.Version, 50, "version");
        var content = Optional(request.Content, 10000, "content");

        if (await repository.TemplateVersionExistsAsync(id, versionValue, cancellationToken))
        {
            throw new DocumentManagementConflictException(
                $"TemplateId={id} 已存在 version={versionValue}。");
        }

        var now = DateTime.UtcNow;
        var version = new DocumentTemplateVersion
        {
            DocumentTemplateId = id,
            Version = versionValue,
            Content = content,
            Sha256 = ComputeSha256(content ?? string.Empty),
            CreatedAtUtc = now
        };

        await repository.AddVersionAsync(version, cancellationToken);
        template.Versions.Add(version);
        if (request.Activate)
        {
            template.ActiveVersion = version;
        }
        template.UpdatedAtUtc = now;

        await repository.SaveChangesAsync(cancellationToken);
        var saved = await repository.GetTemplateAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"新增 TemplateId={id} 版本後無法重新讀取資料。");
        return MapTemplateDetail(saved);
    }

    public async Task<DocumentTemplateDetailDto?> SetActiveVersionAsync(
        int id,
        DocumentTemplateActivateDto request,
        CancellationToken cancellationToken)
    {
        var template = await repository.GetTemplateForUpdateAsync(id, cancellationToken);
        if (template is null) return null;

        var version = template.Versions.SingleOrDefault(x => x.Id == request.VersionId);
        if (version is null)
        {
            throw new DocumentManagementConflictException(
                $"VersionId={request.VersionId} 不屬於 TemplateId={id}。");
        }

        template.ActiveVersionId = version.Id;
        template.ActiveVersion = version;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await repository.SaveChangesAsync(cancellationToken);

        var saved = await repository.GetTemplateAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"更新 TemplateId={id} 啟用版本後無法重新讀取資料。");
        return MapTemplateDetail(saved);
    }

    private static CaseDocumentDto MapCaseDocument(ApplicationDocument document)
    {
        var quote = document.Application.Quote;
        return new CaseDocumentDto(
            document.Id,
            document.ApplicationId,
            document.Application.ApplicationNumber,
            quote.CustomerId,
            quote.Customer.Name,
            quote.VehicleId,
            quote.Vehicle.LicensePlate,
            document.DocumentType,
            GetDisplayName(document.DocumentType),
            document.Status,
            document.OriginalFileName,
            document.ContentType,
            document.SizeBytes,
            document.Sha256,
            document.ReturnReason,
            AsUtc(document.UploadedAtUtc),
            document.ReturnedAtUtc.HasValue ? AsUtc(document.ReturnedAtUtc.Value) : null,
            AsUtc(document.UpdatedAtUtc));
    }

    private static DocumentTemplateSummaryDto MapTemplateSummary(DocumentTemplate template) =>
        new(
            template.Id,
            template.Code,
            template.Name,
            template.Description,
            template.ActiveVersionId,
            template.ActiveVersion?.Version,
            template.Versions.Count,
            AsUtc(template.CreatedAtUtc),
            AsUtc(template.UpdatedAtUtc));

    private static DocumentTemplateDetailDto MapTemplateDetail(DocumentTemplate template) =>
        new(
            template.Id,
            template.Code,
            template.Name,
            template.Description,
            template.ActiveVersionId,
            template.ActiveVersion?.Version,
            template.Versions
                .OrderByDescending(x => x.CreatedAtUtc)
                .ThenByDescending(x => x.Id)
                .Select(x => new DocumentTemplateVersionDto(
                    x.Id,
                    x.Version,
                    x.Content,
                    x.Sha256,
                    AsUtc(x.CreatedAtUtc)))
                .ToList(),
            AsUtc(template.CreatedAtUtc),
            AsUtc(template.UpdatedAtUtc));

    private static string GetDisplayName(string documentType) => documentType switch
    {
        "identity_document" => "身分證正反面",
        "vehicle_registration" => "行車執照影本",
        _ => documentType
    };

    private static string Required(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DocumentManagementValidationException($"{field} 為必填欄位。");
        }
        if (normalized.Length > maxLength)
        {
            throw new DocumentManagementValidationException($"{field} 不可超過 {maxLength} 字元。");
        }
        return normalized;
    }

    private static string? Optional(string? value, int maxLength, string field)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new DocumentManagementValidationException($"{field} 不可超過 {maxLength} 字元。");
        }
        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ComputeSha256(string content) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    private static DateTime AsUtc(DateTime value) =>
        DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
