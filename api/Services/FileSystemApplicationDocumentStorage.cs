using System.Security.Cryptography;

namespace CarInsurance.Api.Services;

public sealed class FileSystemApplicationDocumentStorage : IApplicationDocumentStorage
{
    private readonly string rootPath;

    public FileSystemApplicationDocumentStorage(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["DocumentStorage:Root"];
        rootPath = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "Data", "uploads")
            : Path.GetFullPath(configuredRoot);
    }

    public async Task<StoredApplicationDocument> SaveAsync(
        int applicationId,
        string extension,
        Stream source,
        CancellationToken cancellationToken)
    {
        var directory = GetApplicationDirectory(applicationId);
        Directory.CreateDirectory(directory);

        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var path = Path.Combine(directory, storedFileName);

        await using var target = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long size = 0;

        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                hash.AppendData(buffer, 0, read);
                size += read;
            }

            await target.FlushAsync(cancellationToken);
            return new StoredApplicationDocument(
                storedFileName,
                size,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
        }
        catch
        {
            await target.DisposeAsync();
            File.Delete(path);
            throw;
        }
    }

    public async Task<byte[]?> ReadAsync(
        int applicationId,
        string storedFileName,
        CancellationToken cancellationToken)
    {
        var path = GetFilePath(applicationId, storedFileName);
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
        var path = GetFilePath(applicationId, storedFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetApplicationDirectory(int applicationId) =>
        Path.Combine(rootPath, applicationId.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private string GetFilePath(int applicationId, string storedFileName)
    {
        var safeName = Path.GetFileName(storedFileName);
        if (!string.Equals(safeName, storedFileName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Stored file name is invalid.");
        }

        return Path.Combine(GetApplicationDirectory(applicationId), safeName);
    }
}
