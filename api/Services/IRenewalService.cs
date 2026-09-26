using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IRenewalService
{
    Task<IReadOnlyList<RenewalListItemDto>> GetEligibleAsync(CancellationToken cancellationToken);
    Task<QuoteDto> CreateQuoteAsync(int policyId, CancellationToken cancellationToken);
}
