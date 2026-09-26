namespace CarInsurance.Api.Dtos;

public sealed record VehicleDto(
    int Id,
    string LicensePlate,
    string Vin,
    string MakeModelCode,
    DateTime? ManufactureDate,
    string UsageType,
    int CustomerId,
    string CustomerName);

public sealed record VehicleListResponse(
    IReadOnlyList<VehicleDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
