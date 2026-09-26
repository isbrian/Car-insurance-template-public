namespace CarInsurance.Api.Services.Models;

public sealed record CustomerSummary(
    int Id,
    string Name,
    string NationalId,
    string Phone,
    string? Email,
    DateTime? BirthDate,
    string? Address,
    bool IsVerified,
    int ClaimRecordLevel,
    DateTime CreatedAtUtc,
    int VehicleCount);

public sealed record CustomerDetails(
    int Id,
    string Name,
    string NationalId,
    string Phone,
    string? Email,
    DateTime? BirthDate,
    string? Address,
    bool IsVerified,
    int ClaimRecordLevel,
    DateTime CreatedAtUtc,
    IReadOnlyList<VehicleSummary> Vehicles);

public sealed record VehicleSummary(
    int Id,
    string LicensePlate,
    string Vin,
    string MakeModelCode,
    int ManufactureYear,
    string UsageType);
