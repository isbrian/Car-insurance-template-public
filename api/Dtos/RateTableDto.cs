using System.Text.Json;

namespace CarInsurance.Api.Dtos;

public sealed record RateTableDto(
    int RateVersionId,
    string SourceVersion,
    bool IsProvisional,
    string DisplayWarning,
    JsonElement Data,
    DateTime ImportedAtUtc);
