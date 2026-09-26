using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/document-templates")]
public sealed class DocumentTemplatesController(IDocumentManagementService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DocumentTemplateSummaryDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetTemplatesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<DocumentTemplateDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await service.GetTemplateAsync(id, cancellationToken);
        return result is null ? NotFoundTemplate(id) : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DocumentTemplateDetailDto>> Create(
        DocumentTemplateCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.CreateTemplateAsync(request, cancellationToken);
            return Created($"/api/document-templates/{result.Id}", result);
        }
        catch (DocumentManagementValidationException ex) { return Invalid(ex.Message); }
        catch (DocumentManagementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (DbUpdateException) { return ConflictProblem("範本資料與目前資料狀態衝突。"); }
    }

    [HttpPost("{id:int}/versions")]
    public async Task<ActionResult<DocumentTemplateDetailDto>> AddVersion(
        int id,
        DocumentTemplateVersionCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.AddVersionAsync(id, request, cancellationToken);
            return result is null
                ? NotFoundTemplate(id)
                : Created($"/api/document-templates/{id}", result);
        }
        catch (DocumentManagementValidationException ex) { return Invalid(ex.Message); }
        catch (DocumentManagementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (DbUpdateException) { return ConflictProblem("範本版本與目前資料狀態衝突。"); }
    }

    [HttpPut("{id:int}/active-version")]
    public async Task<ActionResult<DocumentTemplateDetailDto>> SetActiveVersion(
        int id,
        DocumentTemplateActivateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.SetActiveVersionAsync(id, request, cancellationToken);
            return result is null ? NotFoundTemplate(id) : Ok(result);
        }
        catch (DocumentManagementValidationException ex) { return Invalid(ex.Message); }
        catch (DocumentManagementConflictException ex) { return ConflictProblem(ex.Message); }
        catch (DbUpdateException) { return ConflictProblem("啟用版本與目前資料狀態衝突。"); }
    }

    private ActionResult Invalid(string detail) =>
        Problem(statusCode: 400, title: "文件範本資料無效", detail: detail);

    private ActionResult ConflictProblem(string detail) =>
        Problem(statusCode: 409, title: "文件範本資料衝突", detail: detail);

    private ActionResult NotFoundTemplate(int id) =>
        Problem(statusCode: 404, title: "文件範本不存在", detail: $"找不到 Id={id} 的文件範本。");
}
