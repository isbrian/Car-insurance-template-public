using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record InsuranceProductDto(
    int Id,
    int InsuranceTypeId,
    string InsuranceTypeCode,
    string InsuranceTypeName,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc);

public class InsuranceProductCreateDto
{
    [Range(1, int.MaxValue)]
    public int InsuranceTypeId { get; set; }

    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class InsuranceProductUpdateDto : InsuranceProductCreateDto
{
}
