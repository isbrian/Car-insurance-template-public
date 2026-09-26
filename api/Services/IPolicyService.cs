using CarInsurance.Api.Dtos;
using CarInsurance.Api.Services.Models;

namespace CarInsurance.Api.Services;

public interface IPolicyService
{
    Task<PolicyDto> IssueAsync(
        int applicationId,
        AuditActorContext auditActor,
        CancellationToken cancellationToken);
    Task<PolicyListResponse> SearchAsync(string? keyword, string? status, int page, int pageSize, CancellationToken cancellationToken);
    Task<PolicyDto?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<PolicyDetailsDto?> GetDetailsAsync(int id, CancellationToken cancellationToken);
    Task<PolicyTimelineDto?> GetTimelineAsync(int id, CancellationToken cancellationToken);
    Task<PolicyDto?> GetByApplicationIdAsync(int applicationId, CancellationToken cancellationToken);
    Task<PolicyDocumentContentDto?> GetDocumentAsync(int id, CancellationToken cancellationToken);
}
