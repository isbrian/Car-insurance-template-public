using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/quotes")]
public sealed class QuotesController(IQuoteService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuoteDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuoteDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var quote = await service.GetByIdAsync(id, cancellationToken);
        return quote is null
            ? Problem(statusCode: 404, title: "報價不存在", detail: $"找不到 Id={id} 的報價。")
            : Ok(quote);
    }

    [HttpPost]
    [Authorize(Policy = AppPolicies.QuoteCreate)]
    public async Task<ActionResult<QuoteDto>> Create(QuoteCreateDto request, CancellationToken cancellationToken)
    {
        try
        {
            var quote = await service.CreateAsync(request, GetAuditActor(), cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = quote.Id }, quote);
        }
        catch (QuoteValidationException ex)
        {
            return Problem(statusCode: 400, title: "報價資料無效", detail: ex.Message);
        }
        catch (PremiumCalculationValidationException ex)
        {
            return Problem(statusCode: 400, title: "保費試算資料無效", detail: ex.Message);
        }
        catch (QuoteNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "報價關聯資料不存在", detail: ex.Message);
        }
        catch (PremiumCalculationNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "費率資料不存在", detail: ex.Message);
        }
        catch (QuoteConflictException ex)
        {
            return Problem(statusCode: 409, title: "報價資料衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "報價資料衝突", detail: "報價編號或關聯資料與目前資料衝突。");
        }
        catch (PremiumCalculationConfigurationException ex)
        {
            return Problem(statusCode: 500, title: "費率設定錯誤", detail: ex.Message);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuoteDto>> Update(int id, QuoteUpdateDto request, CancellationToken cancellationToken)
    {
        try
        {
            var quote = await service.UpdateAsync(id, request, GetAuditActor(), cancellationToken);
            return quote is null
                ? Problem(statusCode: 404, title: "報價不存在", detail: $"找不到 Id={id} 的報價。")
                : Ok(quote);
        }
        catch (QuoteValidationException ex)
        {
            return Problem(statusCode: 400, title: "報價資料無效", detail: ex.Message);
        }
        catch (PremiumCalculationValidationException ex)
        {
            return Problem(statusCode: 400, title: "保費試算資料無效", detail: ex.Message);
        }
        catch (QuoteNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "報價關聯資料不存在", detail: ex.Message);
        }
        catch (PremiumCalculationNotFoundException ex)
        {
            return Problem(statusCode: 404, title: "費率資料不存在", detail: ex.Message);
        }
        catch (QuoteConflictException ex)
        {
            return Problem(statusCode: 409, title: "報價資料衝突", detail: ex.Message);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "報價資料衝突", detail: "報價編號或關聯資料與目前資料衝突。");
        }
        catch (PremiumCalculationConfigurationException ex)
        {
            return Problem(statusCode: 500, title: "費率設定錯誤", detail: ex.Message);
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: 404, title: "報價不存在", detail: $"找不到 Id={id} 的報價。");
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
}
