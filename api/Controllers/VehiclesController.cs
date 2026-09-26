using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController(IVehicleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<VehicleListResponse>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int? customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "分頁參數無效",
                Detail = "page 必須大於 0，pageSize 必須介於 1 到 100。"
            });
        }

        if (customerId is <= 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "車主參數無效",
                Detail = "customerId 必須大於 0。"
            });
        }

        return Ok(await service.GetAllAsync(
            keyword,
            customerId,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VehicleDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var vehicle = await service.GetByIdAsync(id, cancellationToken);
        return vehicle is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "車輛不存在",
                detail: $"找不到 Id={id} 的車輛。")
            : Ok(vehicle);
    }

    [HttpPost]
    public async Task<ActionResult<VehicleDto>> Create(
        VehicleCreateDto request,
        CancellationToken cancellationToken)
    {
        if (!await service.CustomerExistsAsync(request.CustomerId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "車主不存在",
                detail: $"找不到 CustomerId={request.CustomerId} 的車主。");
        }

        if (await service.ExistsByLicensePlateAsync(request.LicensePlate, null, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車牌已存在",
                detail: "LicensePlate 不可重複。");
        }

        if (await service.ExistsByVinAsync(request.Vin, null, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車身號碼已存在",
                detail: "Vin 不可重複。");
        }

        try
        {
            var vehicle = await service.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = vehicle.Id }, vehicle);
        }
        catch (DbUpdateException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車輛資料衝突",
                detail: "車牌、Vin 或車主關聯與目前資料衝突。");
        }
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<VehicleDto>> Update(
        int id,
        VehicleUpdateDto request,
        CancellationToken cancellationToken)
    {
        if (!await service.CustomerExistsAsync(request.CustomerId, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "車主不存在",
                detail: $"找不到 CustomerId={request.CustomerId} 的車主。");
        }

        if (await service.ExistsByLicensePlateAsync(request.LicensePlate, id, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車牌已存在",
                detail: "LicensePlate 不可重複。");
        }

        if (await service.ExistsByVinAsync(request.Vin, id, cancellationToken))
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車身號碼已存在",
                detail: "Vin 不可重複。");
        }

        try
        {
            var vehicle = await service.UpdateAsync(id, request, cancellationToken);
            return vehicle is null
                ? Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "車輛不存在",
                    detail: $"找不到 Id={id} 的車輛。")
                : Ok(vehicle);
        }
        catch (DbUpdateException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "車輛資料衝突",
                detail: "車牌、Vin 或車主關聯與目前資料衝突。");
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        return await service.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "車輛不存在",
                detail: $"找不到 Id={id} 的車輛。");
    }
}
