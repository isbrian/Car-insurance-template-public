using System.Text.Json;
using CarInsurance.Api.Dtos;
using CarInsurance.Api.Repositories;

namespace CarInsurance.Api.Services;

public sealed class RateTableService(IRateTableRepository repository) : IRateTableService
{
    public async Task<RateTableDto?> GetByRateVersionIdAsync(int rateVersionId, CancellationToken cancellationToken)
    {
        var entity = await repository.GetByRateVersionIdAsync(rateVersionId, cancellationToken);
        if (entity is null) return null;

        using var document = JsonDocument.Parse(entity.JsonData);
        return new RateTableDto(
            entity.RateVersionId,
            entity.SourceVersion,
            entity.IsProvisional,
            entity.DisplayWarning,
            document.RootElement.Clone(),
            entity.ImportedAtUtc);
    }
}
