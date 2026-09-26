namespace CarInsurance.Api.Entities;

public class Vehicle
{
    public int Id { get; set; }

    public string LicensePlate { get; set; } = string.Empty;

    public string Vin { get; set; } = string.Empty;

    public string MakeModelCode { get; set; } = string.Empty;

    public DateTime? ManufactureDate { get; set; }

    public string UsageType { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;
}
