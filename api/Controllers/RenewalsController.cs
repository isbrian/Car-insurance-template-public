using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/renewals")]
public sealed class RenewalsController(IRenewalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RenewalListItemDto>>> GetAll(
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.GetEligibleAsync(cancellationToken));
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "續保狀態衝突", detail: ex.Message);
        }
        catch (RenewalDataIntegrityException ex)
        {
            return Problem(statusCode: 500, title: "續保資料不完整", detail: ex.Message);
        }
    }

    [HttpPost("{policyId:int}/quote")]
    public async Task<ActionResult<QuoteDto>> CreateQuote(
        int policyId,
        CancellationToken cancellationToken)
    {
        try
        {
            var quote = await service.CreateQuoteAsync(policyId, cancellationToken);
            return Created($"/api/quotes/{quote.Id}", quote);
        }
        catch (RenewalNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "保單不存在", detail: ex.Message);
        }
        catch (PremiumCalculationNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "費率資料不存在", detail: ex.Message);
        }
        catch (PremiumCalculationValidationException ex)
        {
            return Problem(statusCode: 400, title: "續保試算資料無效", detail: ex.Message);
        }
        catch (RenewalConflictException ex)
        {
            return Problem(statusCode: 409, title: "續保資料衝突", detail: ex.Message);
        }
        catch (PolicyLifecycleConflictException ex)
        {
            return Problem(statusCode: 409, title: "續保狀態衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "續保資料衝突", detail: "來源保單已建立續保報價，或報價編號與目前資料衝突。");
        }
        catch (RenewalDataIntegrityException ex)
        {
            return Problem(statusCode: 500, title: "續保資料不完整", detail: ex.Message);
        }
        catch (PremiumCalculationConfigurationException ex)
        {
            return Problem(statusCode: 500, title: "費率設定錯誤", detail: ex.Message);
        }
    }
}
