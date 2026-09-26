namespace CarInsurance.Api.Entities;

public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NationalId { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public string? Email { get; set; }

    public DateTime? BirthDate { get; set; }

    public string? Address { get; set; }

    public bool IsVerified { get; set; }

    public int ClaimRecordLevel { get; set; } = 4;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}
