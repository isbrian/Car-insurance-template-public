using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/rate-tables")]
public sealed class RateTablesController(IRateTableService service) : ControllerBase
{
    [HttpGet("{rateVersionId:int}")]
    public async Task<ActionResult<RateTableDto>> GetByRateVersionId(int rateVersionId, CancellationToken cancellationToken)
    {
        if (rateVersionId <= 0)
        {
            return BadRequest(new ProblemDetails { Title = "料金バージョン ID が無効です", Detail = "rateVersionId は 1 以上である必要があります。" });
        }

        var item = await service.GetByRateVersionIdAsync(rateVersionId, cancellationToken);
        return item is null
            ? Problem(statusCode: 404, title: "料金テーブルが存在しません", detail: $"RateVersionId={rateVersionId} の料金テーブルが見つかりません。")
            : Ok(item);
    }
}
