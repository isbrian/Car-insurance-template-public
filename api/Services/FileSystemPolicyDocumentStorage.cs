using System.Security.Cryptography;

namespace CarInsurance.Api.Services;

public sealed class FileSystemPolicyDocumentStorage : IPolicyDocumentStorage
{
    private readonly string rootPath;

    public FileSystemPolicyDocumentStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["PolicyIssuance:StorageRoot"];
        rootPath = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "Data", "policies")
            : Path.GetFullPath(configuredRoot);
    }

    public async Task<StoredPolicyDocument> SaveAsync(
        int applicationId,
        string storedFileName,
        byte[] content,
        CancellationToken cancellationToken)
    {
        var safeName = GetSafeFileName(storedFileName);
        var directory = GetApplicationDirectory(applicationId);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, safeName);

        await File.WriteAllBytesAsync(path, content, cancellationToken);
        var hash = SHA256.HashData(content);
        return new StoredPolicyDocument(
            safeName,
            content.LongLength,
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    public async Task<byte[]?> ReadAsync(
        int applicationId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(GetApplicationDirectory(applicationId), GetSafeFileName(storedFileName));
        return File.Exists(path)
            ? await File.ReadAllBytesAsync(path, cancellationToken)
            : null;
    }

    public Task DeleteAsync(
        int applicationId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = Path.Combine(GetApplicationDirectory(applicationId), GetSafeFileName(storedFileName));
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetApplicationDirectory(int applicationId) =>
        Path.Combine(rootPath, applicationId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static string GetSafeFileName(string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        if (string.IsNullOrWhiteSpace(safeName) || !string.Equals(safeName, storedFileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Policy stored file name is invalid.");
        }

        return safeName;
    }
}
