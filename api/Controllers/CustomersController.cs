using CarInsurance.Api.Authorization;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services;
using CarInsurance.Api.Services.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarInsurance.Api.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(ICustomerService customerService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(CustomerListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CustomerListResponse>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "分頁參數無效",
                detail: "page 必須大於 0，pageSize 必須介於 1 至 100。",
                type: "https://httpstatuses.com/400");
        }

        return Ok(await customerService.GetAllAsync(
            keyword,
            page,
            pageSize,
            cancellationToken));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CustomerDetails), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerDetails>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var customer = await customerService.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost("{id:int}/pii/unmask")]
    [Authorize(Policy = AppPolicies.PiiUnmask)]
    [ProducesResponseType(typeof(CustomerPiiUnmaskResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerPiiUnmaskResponseDto>> UnmaskPii(
        int id,
        CustomerPiiUnmaskRequestDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await customerService.UnmaskPiiAsync(
                id,
                request,
                GetAuditActor(),
                cancellationToken);
            return result is null
                ? Problem(statusCode: 404, title: "客戶不存在", detail: $"找不到 Id={id} 的客戶。")
                : Ok(result);
        }
        catch (CustomerPiiUnmaskValidationException ex)
        {
            return Problem(statusCode: 400, title: "解遮罩理由無效", detail: ex.Message);
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Create(
        CustomerCreateDto request,
        CancellationToken cancellationToken)
    {
        if (await customerService.ExistsByNationalIdAsync(
                request.NationalId,
                null,
                cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "身分證字號已存在",
                Detail = "請使用其他身分證字號。"
            });
        }

        var customer = await customerService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CustomerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CustomerDto>> Update(
        int id,
        CustomerCreateDto request,
        CancellationToken cancellationToken)
    {
        if (await customerService.ExistsByNationalIdAsync(
                request.NationalId,
                id,
                cancellationToken))
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "身分證字號已存在",
                Detail = "請使用其他身分證字號。"
            });
        }

        var customer = await customerService.UpdateAsync(id, request, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    private AuditActorContext GetAuditActor() =>
        new(
            User.Identity?.Name ?? User.FindFirst("name")?.Value ?? string.Empty,
            User.FindFirst("role")?.Value ?? string.Empty,
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
}
