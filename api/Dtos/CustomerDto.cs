using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record CustomerDto(
    int Id,
    string Name,
    string NationalId,
    string Phone,
    string? Email,
    DateTime? BirthDate,
    string? Address,
    bool IsVerified,
    int ClaimRecordLevel,
    DateTime CreatedAtUtc);

public sealed record CustomerListResponse(
    IReadOnlyList<CustomerDto> Items,
    int Total,
    int Page,
    int PageSize);

public sealed record CustomerPiiUnmaskRequestDto(
    [Required, MaxLength(500)] string Reason);

public sealed record CustomerPiiUnmaskResponseDto(
    int Id,
    string NationalId,
    string Phone);
