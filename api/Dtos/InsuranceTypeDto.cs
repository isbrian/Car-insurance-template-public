using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record InsuranceTypeDto(
    int Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc);

public class InsuranceTypeCreateDto
{
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class InsuranceTypeUpdateDto : InsuranceTypeCreateDto
{
}
