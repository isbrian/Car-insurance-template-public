using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/documents")]
public sealed class DocumentsController(IDocumentManagementService service) : ControllerBase
{
    [HttpGet("cases")]
    public async Task<ActionResult<CaseDocumentListResponse>> GetCases(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        [FromQuery] string? documentType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await service.GetCaseDocumentsAsync(
                keyword,
                status,
                documentType,
                page,
                pageSize,
                cancellationToken));
        }
        catch (DocumentManagementValidationException ex)
        {
            return Problem(statusCode: 400, title: "文件查詢條件無效", detail: ex.Message);
        }
    }
}
