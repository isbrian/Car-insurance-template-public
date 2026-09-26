using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/insurance-products")]
public sealed class InsuranceProductsController(IInsuranceCatalogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InsuranceProductDto>>> GetAll([FromQuery] int? insuranceTypeId, CancellationToken cancellationToken)
    {
        if (insuranceTypeId is <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "險種參數無效", Detail = "insuranceTypeId 必須大於 0。" });
        }

        return Ok(await service.GetInsuranceProductsAsync(insuranceTypeId, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InsuranceProductDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetInsuranceProductAsync(id, cancellationToken);
        return item is null
            ? Problem(statusCode: 404, title: "商品方案不存在", detail: $"找不到 Id={id} 的商品方案。")
            : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<InsuranceProductDto>> Create(InsuranceProductCreateDto request, CancellationToken cancellationToken)
    {
        if (!await service.InsuranceTypeExistsAsync(request.InsuranceTypeId, cancellationToken))
        {
            return Problem(statusCode: 404, title: "險種不存在", detail: $"找不到 InsuranceTypeId={request.InsuranceTypeId} 的險種。");
        }
        if (await service.InsuranceProductCodeExistsAsync(request.Code, null, cancellationToken))
        {
            return Problem(statusCode: 409, title: "商品代碼已存在", detail: "Code 不可重複。");
        }

        try
        {
            var item = await service.CreateInsuranceProductAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "商品資料衝突", detail: "商品代碼或險種關聯與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InsuranceProductDto>> Update(int id, InsuranceProductUpdateDto request, CancellationToken cancellationToken)
    {
        if (!await service.InsuranceTypeExistsAsync(request.InsuranceTypeId, cancellationToken))
        {
            return Problem(statusCode: 404, title: "險種不存在", detail: $"找不到 InsuranceTypeId={request.InsuranceTypeId} 的險種。");
        }
        if (await service.InsuranceProductCodeExistsAsync(request.Code, id, cancellationToken))
        {
            return Problem(statusCode: 409, title: "商品代碼已存在", detail: "Code 不可重複。");
        }

        try
        {
            var item = await service.UpdateInsuranceProductAsync(id, request, cancellationToken);
            return item is null
                ? Problem(statusCode: 404, title: "商品方案不存在", detail: $"找不到 Id={id} 的商品方案。")
                : Ok(item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "商品資料衝突", detail: "商品代碼或險種關聯與目前資料衝突。");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (await service.InsuranceProductHasRateVersionsAsync(id, cancellationToken))
        {
            return Problem(statusCode: 409, title: "商品仍被費率版本使用", detail: "請先移除費率版本後再刪除商品方案。");
        }

        return await service.DeleteInsuranceProductAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: 404, title: "商品方案不存在", detail: $"找不到 Id={id} 的商品方案。");
    }
}
