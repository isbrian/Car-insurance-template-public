namespace CarInsurance.Api.Entities;

public sealed class UserAccount
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string? PasswordResetTokenHash { get; set; }
    public DateTime? PasswordResetExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
