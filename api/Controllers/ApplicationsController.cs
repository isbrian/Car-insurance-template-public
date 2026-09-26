using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/applications")]
public sealed class ApplicationsController(IApplicationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InsuranceApplicationDto>>> GetAll(
        CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InsuranceApplicationDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var application = await service.GetByIdAsync(id, cancellationToken);
        return application is null
            ? NotFoundProblem(id)
            : Ok(application);
    }

    [HttpGet("by-quote/{quoteId:int}")]
    public async Task<ActionResult<InsuranceApplicationDto>> GetByQuoteId(
        int quoteId,
        CancellationToken cancellationToken)
    {
        var application = await service.GetByQuoteIdAsync(quoteId, cancellationToken);
        return application is null
            ? Problem(
                statusCode: 404,
                title: "投保申請不存在",
                detail: $"找不到 QuoteId={quoteId} 的投保申請。")
            : Ok(application);
    }

    [HttpPost]
    public async Task<ActionResult<InsuranceApplicationDto>> Create(
        ApplicationCreateDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var application = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = application.Id }, application);
        }
        catch (ApplicationNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "報價不存在", detail: ex.Message);
        }
        catch (ApplicationConflictException ex)
        {
            return Problem(statusCode: 409, title: "投保申請衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "投保申請衝突", detail: "投保申請編號或報價關聯與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}/applicant")]
    public Task<ActionResult<InsuranceApplicationDto>> UpdateApplicant(
        int id,
        ApplicantStepDto request,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.UpdateApplicantAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/insured")]
    public Task<ActionResult<InsuranceApplicationDto>> UpdateInsured(
        int id,
        InsuredStepDto request,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.UpdateInsuredAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/beneficiaries")]
    public Task<ActionResult<InsuranceApplicationDto>> UpdateBeneficiaries(
        int id,
        BeneficiariesStepDto request,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.UpdateBeneficiariesAsync(id, request, cancellationToken));

    [HttpPut("{id:int}/signature-status")]
    public Task<ActionResult<InsuranceApplicationDto>> UpdateSignatureStatus(
        int id,
        SignatureStatusStepDto request,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.UpdateSignatureStatusAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/signatures")]
    public Task<ActionResult<InsuranceApplicationDto>> Sign(
        int id,
        ApplicationSignatureCreateDto request,
        CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();
        return RunUpdate(id, () => service.SignAsync(id, request, ipAddress, userAgent, cancellationToken));
    }

    [HttpDelete("{id:int}/signatures/{signerRole}")]
    public Task<ActionResult<InsuranceApplicationDto>> ClearSignature(
        int id,
        string signerRole,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.ClearSignatureAsync(id, signerRole, cancellationToken));

    [HttpGet("{id:int}/signatures/{signerRole}/image")]
    public async Task<IActionResult> GetSignatureImage(
        int id,
        string signerRole,
        CancellationToken cancellationToken)
    {
        try
        {
            var image = await service.GetSignatureImageAsync(id, signerRole, cancellationToken);
            if (image is null)
            {
                return NotFoundProblem(id);
            }

            Response.Headers.ETag = $"\"{image.Sha256}\"";
            return File(image.Bytes, "image/png");
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保申請資料無效", detail: ex.Message);
        }
        catch (ApplicationSignatureNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "電子簽章不存在", detail: ex.Message);
        }
    }

    [HttpGet("{id:int}/documents")]
    public async Task<ActionResult<IReadOnlyList<ApplicationDocumentDto>>> GetDocuments(
        int id,
        CancellationToken cancellationToken)
    {
        var documents = await service.GetDocumentsAsync(id, cancellationToken);
        return documents is null ? NotFoundProblem(id) : Ok(documents);
    }

    [HttpPost("{id:int}/documents/{documentType}")]
    public async Task<ActionResult<IReadOnlyList<ApplicationDocumentDto>>> UploadDocument(
        int id,
        string documentType,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = file.OpenReadStream();
            var documents = await service.UploadDocumentAsync(
                id,
                documentType,
                file.FileName,
                file.ContentType,
                file.Length,
                stream,
                GetAuditActor(),
                cancellationToken);
            return documents is null ? NotFoundProblem(id) : Ok(documents);
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保文件無效", detail: ex.Message);
        }
        catch (ApplicationConflictException ex)
        {
            return Problem(statusCode: 409, title: "投保文件衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "投保文件衝突", detail: "文件資料與目前資料狀態衝突。");
        }
    }

    [HttpGet("{id:int}/documents/{documentType}/content")]
    public async Task<IActionResult> GetDocumentContent(
        int id,
        string documentType,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await service.GetDocumentContentAsync(id, documentType, cancellationToken);
            if (content is null)
            {
                return NotFoundProblem(id);
            }

            Response.Headers.ETag = $"\"{content.Sha256}\"";
            return File(content.Bytes, content.ContentType);
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保文件無效", detail: ex.Message);
        }
        catch (ApplicationDocumentNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "投保文件不存在", detail: ex.Message);
        }
    }

    [HttpPut("{id:int}/documents/{documentType}/return")]
    public async Task<ActionResult<IReadOnlyList<ApplicationDocumentDto>>> ReturnDocument(
        int id,
        string documentType,
        ApplicationDocumentReturnDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var documents = await service.ReturnDocumentAsync(id, documentType, request, GetAuditActor(), cancellationToken);
            return documents is null ? NotFoundProblem(id) : Ok(documents);
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保文件無效", detail: ex.Message);
        }
        catch (ApplicationDocumentNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "投保文件不存在", detail: ex.Message);
        }
    }

    [HttpPut("{id:int}/document-confirmation")]
    public Task<ActionResult<InsuranceApplicationDto>> UpdateDocumentConfirmation(
        int id,
        DocumentConfirmationStepDto request,
        CancellationToken cancellationToken) =>
        RunUpdate(id, () => service.UpdateDocumentConfirmationAsync(id, request, cancellationToken));

    [HttpPost("{id:int}/submit")]
    [Authorize(Policy = AppPolicies.ApplicationSubmit)]
    public async Task<ActionResult<InsuranceApplicationDto>> Submit(
        int id,
        CancellationToken cancellationToken)
    {
        try
        {
            var application = await service.SubmitAsync(id, GetAuditActor(), cancellationToken);
            return application is null ? NotFoundProblem(id) : Ok(application);
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保申請資料無效", detail: ex.Message);
        }
        catch (ApplicationConflictException ex)
        {
            return Problem(statusCode: 409, title: "投保申請衝突", detail: ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "保單狀態轉換衝突", detail: ex.Message);
        }
        catch (PolicyLifecycleNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單生命週期關聯不存在", detail: ex.Message);
        }
    }

    private async Task<ActionResult<InsuranceApplicationDto>> RunUpdate(
        int id,
        Func<Task<InsuranceApplicationDto?>> action)
    {
        try
        {
            var application = await action();
            return application is null ? NotFoundProblem(id) : Ok(application);
        }
        catch (ApplicationValidationException ex)
        {
            return Problem(statusCode: 400, title: "投保申請資料無效", detail: ex.Message);
        }
        catch (ApplicationConflictException ex)
        {
            return Problem(statusCode: 409, title: "投保申請衝突", detail: ex.Message);
        }
        catch (ApplicationSignatureNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "電子簽章不存在", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "投保申請衝突", detail: "投保申請資料與目前資料狀態衝突。");
        }
    }

    private AuditActorContext GetAuditActor()
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        return new AuditActorContext(
            authenticated
                ? User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty
                : "anonymous",
            authenticated
                ? User.FindFirst("role")?.Value ?? string.Empty
                : "anonymous",
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private ObjectResult NotFoundProblem(int id) =>
        Problem(
            statusCode: 404,
            title: "投保申請不存在",
            detail: $"找不到 Id={id} 的投保申請。");
}
