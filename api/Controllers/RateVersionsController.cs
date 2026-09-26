using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/rate-versions")]
public sealed class RateVersionsController(IInsuranceCatalogService service) : ControllerBase
{
    private static readonly HashSet<string> AllowedConfidence = new(StringComparer.OrdinalIgnoreCase)
    {
        "official", "derived", "estimated", "provisional"
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RateVersionDto>>> GetAll([FromQuery] int? insuranceProductId, CancellationToken cancellationToken)
    {
        if (insuranceProductId is <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "商品參數無效", Detail = "insuranceProductId 必須大於 0。" });
        }

        return Ok(await service.GetRateVersionsAsync(insuranceProductId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RateVersionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetRateVersionAsync(id, cancellationToken);
        return item is null
            ? Problem(statusCode: 404, title: "費率版本不存在", detail: $"找不到 Id={id} 的費率版本。")
            : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<RateVersionDto>> Create(RateVersionCreateDto request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        if (!await service.InsuranceProductExistsAsync(request.InsuranceProductId, cancellationToken))
        {
            return Problem(statusCode: 404, title: "商品方案不存在", detail: $"找不到 InsuranceProductId={request.InsuranceProductId} 的商品方案。");
        }
        if (await service.RateVersionExistsAsync(request.InsuranceProductId, request.Version, null, cancellationToken))
        {
            return Problem(statusCode: 409, title: "費率版本已存在", detail: "同一商品下 Version 不可重複。");
        }

        try
        {
            var item = await service.CreateRateVersionAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "費率版本資料衝突", detail: "版本或商品關聯與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<RateVersionDto>> Update(int id, RateVersionUpdateDto request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null) return validation;
        if (!await service.InsuranceProductExistsAsync(request.InsuranceProductId, cancellationToken))
        {
            return Problem(statusCode: 404, title: "商品方案不存在", detail: $"找不到 InsuranceProductId={request.InsuranceProductId} 的商品方案。");
        }
        if (await service.RateVersionExistsAsync(request.InsuranceProductId, request.Version, id, cancellationToken))
        {
            return Problem(statusCode: 409, title: "費率版本已存在", detail: "同一商品下 Version 不可重複。");
        }

        try
        {
            var item = await service.UpdateRateVersionAsync(id, request, cancellationToken);
            return item is null
                ? Problem(statusCode: 404, title: "費率版本不存在", detail: $"找不到 Id={id} 的費率版本。")
                : Ok(item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "費率版本資料衝突", detail: "版本或商品關聯與目前資料衝突。");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await service.DeleteRateVersionAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: 404, title: "費率版本不存在", detail: $"找不到 Id={id} 的費率版本。");

    private ActionResult? Validate(RateVersionCreateDto request)
    {
        if (request.EffectiveTo.HasValue && request.EffectiveTo.Value < request.EffectiveFrom)
        {
            return BadRequest(new ProblemDetails { Title = "生效期間無效", Detail = "EffectiveTo 不得早於 EffectiveFrom。" });
        }
        if (!AllowedConfidence.Contains(request.Confidence.Trim()))
        {
            return BadRequest(new ProblemDetails { Title = "Confidence 無效", Detail = "Confidence 僅允許 official、derived、estimated、provisional。" });
        }
        return null;
    }
}
