using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IQuoteService
{
    Task<IReadOnlyList<QuoteDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<QuoteDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<QuoteDto> CreateAsync(
        QuoteCreateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<QuoteDto?> UpdateAsync(
        int id,
        QuoteUpdateDto request,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
