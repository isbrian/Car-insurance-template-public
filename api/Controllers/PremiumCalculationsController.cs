using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/premium-calculations")]
public sealed class PremiumCalculationsController(IPremiumCalculationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PremiumCalculationResponseDto>> Calculate(
        PremiumCalculationRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await service.CalculateAsync(request, cancellationToken));
        }
        catch (PremiumCalculationValidationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "試算入力が無効です",
                Detail = exception.Message
            });
        }
        catch (PremiumCalculationNotFoundException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "料金テーブルが存在しません",
                detail: exception.Message);
        }
        catch (PremiumCalculationConfigurationException exception)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "料金設定が不正です",
                detail: exception.Message);
        }
    }
}
