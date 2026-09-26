using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record CaseDocumentDto(
    int Id,
    int ApplicationId,
    string ApplicationNumber,
    int CustomerId,
    string CustomerName,
    int VehicleId,
    string LicensePlate,
    string DocumentType,
    string DisplayName,
    string Status,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string? ReturnReason,
    DateTime UploadedAtUtc,
    DateTime? ReturnedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record CaseDocumentListResponse(
    IReadOnlyList<CaseDocumentDto> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DocumentTemplateCreateDto(
    [Required, MaxLength(50)] string Code,
    [Required, MaxLength(150)] string Name,
    [MaxLength(500)] string? Description);

public sealed record DocumentTemplateVersionCreateDto(
    [Required, MaxLength(50)] string Version,
    [MaxLength(10000)] string? Content,
    bool Activate = false);

public sealed record DocumentTemplateActivateDto(
    [Range(1, int.MaxValue)] int VersionId);

public sealed record DocumentTemplateVersionDto(
    int Id,
    string Version,
    string? Content,
    string Sha256,
    DateTime CreatedAtUtc);

public sealed record DocumentTemplateSummaryDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ActiveVersionId,
    string? ActiveVersion,
    int VersionCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record DocumentTemplateDetailDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    int? ActiveVersionId,
    string? ActiveVersion,
    IReadOnlyList<DocumentTemplateVersionDto> Versions,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
