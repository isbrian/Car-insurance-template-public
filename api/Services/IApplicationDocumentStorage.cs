namespace CarInsurance.Api.Services;

public sealed record StoredApplicationDocument(
    string StoredFileName,
    long SizeBytes,
    string Sha256);

public interface IApplicationDocumentStorage
{
    Task<StoredApplicationDocument> SaveAsync(
        int applicationId,
        string extension,
        Stream source,
        CancellationToken cancellationToken);

    Task<byte[]?> ReadAsync(
        int applicationId,
        string storedFileName,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        int applicationId,
        string storedFileName,
        CancellationToken cancellationToken);
}
