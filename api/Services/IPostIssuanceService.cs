using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IPostIssuanceService
{
    Task<PolicyPostIssuanceTimelineDto> ProcessAsync(int policyId, CancellationToken cancellationToken);
    Task<PolicyPostIssuanceTimelineDto?> GetTimelineAsync(int policyId, CancellationToken cancellationToken);
}
