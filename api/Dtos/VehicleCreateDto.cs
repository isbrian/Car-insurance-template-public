using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public class VehicleCreateDto
{
    [Required]
    public string LicensePlate { get; set; } = string.Empty;

    [Required]
    public string Vin { get; set; } = string.Empty;

    [Required]
    public string MakeModelCode { get; set; } = string.Empty;

    public DateTime? ManufactureDate { get; set; }

    [Required]
    public string UsageType { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int CustomerId { get; set; }
}
