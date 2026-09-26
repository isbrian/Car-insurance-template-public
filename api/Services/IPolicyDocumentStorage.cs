namespace CarInsurance.Api.Services;

public sealed record StoredPolicyDocument(
    string StoredFileName,
    long SizeBytes,
    string Sha256);

public interface IPolicyDocumentStorage
{
    Task<StoredPolicyDocument> SaveAsync(
        int applicationId,
        string storedFileName,
        byte[] content,
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
