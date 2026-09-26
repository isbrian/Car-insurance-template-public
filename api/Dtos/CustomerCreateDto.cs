using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed class CustomerCreateDto
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Za-z][0-9]{9}$")]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress]
    public string? Email { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? Address { get; set; }
}
