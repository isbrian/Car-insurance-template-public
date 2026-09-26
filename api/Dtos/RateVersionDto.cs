using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record RateVersionDto(
    int Id,
    int InsuranceProductId,
    string InsuranceProductCode,
    string InsuranceProductName,
    string Version,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    string Confidence,
    bool IsActive,
    DateTime CreatedAtUtc);

public class RateVersionCreateDto
{
    [Range(1, int.MaxValue)]
    public int InsuranceProductId { get; set; }

    [Required, MaxLength(50)]
    public string Version { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    public DateTime? EffectiveTo { get; set; }

    [Required, MaxLength(20)]
    public string Confidence { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class RateVersionUpdateDto : RateVersionCreateDto
{
}
