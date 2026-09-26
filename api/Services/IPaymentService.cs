using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IPaymentService
{
    Task<IReadOnlyList<PaymentDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<PaymentDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PaymentDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<PaymentDto> CreateAsync(PaymentCreateDto request, CancellationToken cancellationToken);
    Task<PaymentDto?> UpdateAsync(int id, PaymentUpdateDto request, CancellationToken cancellationToken);
    Task<PaymentDto?> ConfirmAsync(
        int id,
        PaymentConfirmDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<PaymentDto?> MarkOverdueAsync(int id, CancellationToken cancellationToken);
    Task<PaymentSyncOverdueResultDto> SyncOverdueAsync(CancellationToken cancellationToken);
}
