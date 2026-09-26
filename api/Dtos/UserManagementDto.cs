using System.ComponentModel.DataAnnotations;

namespace CarInsurance.Api.Dtos;

public sealed record UserAccountDto(
    int Id,
    string Username,
    string Email,
    string DisplayName,
    string Phone,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed class UserActiveUpdateDto
{
    public bool IsActive { get; set; }
}

public sealed class UserRoleUpdateDto
{
    [Required, MaxLength(30)]
    public string Role { get; set; } = string.Empty;
}
