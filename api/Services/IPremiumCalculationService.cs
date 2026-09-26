using CarInsurance.Api.Dtos;

namespace CarInsurance.Api.Services;

public interface IPremiumCalculationService
{
    Task<PremiumCalculationResponseDto> CalculateAsync(
        PremiumCalculationRequestDto request,
        CancellationToken cancellationToken);
}
