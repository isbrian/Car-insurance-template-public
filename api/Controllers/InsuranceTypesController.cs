using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/insurance-types")]
public sealed class InsuranceTypesController(IInsuranceCatalogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<InsuranceTypeDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await service.GetInsuranceTypesAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<InsuranceTypeDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetInsuranceTypeAsync(id, cancellationToken);
        return item is null
            ? Problem(statusCode: 404, title: "險種不存在", detail: $"找不到 Id={id} 的險種。")
            : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<InsuranceTypeDto>> Create(InsuranceTypeCreateDto request, CancellationToken cancellationToken)
    {
        if (await service.InsuranceTypeCodeExistsAsync(request.Code, null, cancellationToken))
        {
            return Problem(statusCode: 409, title: "險種代碼已存在", detail: "Code 不可重複。");
        }

        try
        {
            var item = await service.CreateInsuranceTypeAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = item.Id }, item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "險種資料衝突", detail: "險種代碼與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<InsuranceTypeDto>> Update(int id, InsuranceTypeUpdateDto request, CancellationToken cancellationToken)
    {
        if (await service.InsuranceTypeCodeExistsAsync(request.Code, id, cancellationToken))
        {
            return Problem(statusCode: 409, title: "險種代碼已存在", detail: "Code 不可重複。");
        }

        try
        {
            var item = await service.UpdateInsuranceTypeAsync(id, request, cancellationToken);
            return item is null
                ? Problem(statusCode: 404, title: "險種不存在", detail: $"找不到 Id={id} 的險種。")
                : Ok(item);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: 409, title: "險種資料衝突", detail: "險種代碼與目前資料衝突。");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        if (await service.InsuranceTypeHasProductsAsync(id, cancellationToken))
        {
            return Problem(statusCode: 409, title: "險種仍被商品使用", detail: "請先移除或調整所屬商品方案後再刪除險種。");
        }

        return await service.DeleteInsuranceTypeAsync(id, cancellationToken)
            ? NoContent()
            : Problem(statusCode: 404, title: "險種不存在", detail: $"找不到 Id={id} 的險種。");
    }
}
