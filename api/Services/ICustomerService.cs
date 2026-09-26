using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface ICustomerService
{
    Task<CustomerListResponse> GetAllAsync(
        string? keyword,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CustomerDetails?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<CustomerPiiUnmaskResponseDto?> UnmaskPiiAsync(
        int id,
        CustomerPiiUnmaskRequestDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<CustomerDto> CreateAsync(CustomerCreateDto request, CancellationToken cancellationToken);
    Task<CustomerDto?> UpdateAsync(int id, CustomerCreateDto request, CancellationToken cancellationToken);
    Task<bool> ExistsByNationalIdAsync(string nationalId, int? excludingId, CancellationToken cancellationToken);
}
