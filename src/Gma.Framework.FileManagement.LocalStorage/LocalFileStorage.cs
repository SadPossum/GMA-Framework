namespace Gma.Framework.FileManagement.LocalStorage;

using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Gma.Framework.FileManagement;

internal sealed class LocalFileStorage(
    IHostEnvironment environment,
    IOptions<FileManagementOptions> fileManagementOptions,
    IOptions<LocalFileStorageOptions> localStorageOptions)
    : IFileStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string rootPath = ResolveRootPath(environment.ContentRootPath, localStorageOptions.Value.RootPath);

    public async Task<FileStorageObjectProperties> PutAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateWriteRequest(request, fileManagementOptions.Value);

        string objectPath = this.GetObjectPath(request.Key);
        string metadataPath = MetadataPath(objectPath);
        string directory = Path.GetDirectoryName(objectPath) ?? this.rootPath;
        Directory.CreateDirectory(directory);

        string tempObjectPath = Path.Combine(directory, $".{Path.GetFileName(objectPath)}.{Guid.NewGuid():N}.tmp");
        string tempMetadataPath = Path.Combine(directory, $".{Path.GetFileName(metadataPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (FileStream output = new(
                tempObjectPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await request.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            }

            long writtenLength = new FileInfo(tempObjectPath).Length;
            if (writtenLength != request.ContentLength)
            {
                throw new IOException(
                    $"Stored object length {writtenLength} did not match declared content length {request.ContentLength}.");
            }

            LocalFileStorageMetadata metadata = LocalFileStorageMetadata.From(request);
            await using (FileStream metadataOutput = new(
                tempMetadataPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(metadataOutput, metadata, JsonOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(tempObjectPath, objectPath, overwrite: true);
            File.Move(tempMetadataPath, metadataPath, overwrite: true);

            return metadata.ToProperties(request.Key);
        }
        finally
        {
            DeleteIfExists(tempObjectPath);
            DeleteIfExists(tempMetadataPath);
        }
    }

    public async Task<FileStorageReadResult?> OpenReadAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        FileStorageObjectProperties? properties = await this.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        if (properties is null)
        {
            return null;
        }

        string objectPath = this.GetObjectPath(key);
        return new FileStorageReadResult(
            properties,
            async (destination, copyCancellationToken) =>
            {
                await using FileStream input = new(
                    objectPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                await input.CopyToAsync(destination, copyCancellationToken).ConfigureAwait(false);
            });
    }

    public async Task<FileStorageObjectProperties?> GetPropertiesAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        string objectPath = this.GetObjectPath(key);
        if (!File.Exists(objectPath))
        {
            return null;
        }

        LocalFileStorageMetadata metadata = await ReadMetadataAsync(key, objectPath, cancellationToken)
            .ConfigureAwait(false);
        return metadata.ToProperties(key);
    }

    public Task<bool> DeleteAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        string objectPath = this.GetObjectPath(key);
        string metadataPath = MetadataPath(objectPath);
        bool existed = File.Exists(objectPath);

        DeleteIfExists(objectPath);
        DeleteIfExists(metadataPath);

        return Task.FromResult(existed);
    }

    private static async Task<LocalFileStorageMetadata> ReadMetadataAsync(
        FileStorageObjectKey key,
        string objectPath,
        CancellationToken cancellationToken)
    {
        string metadataPath = MetadataPath(objectPath);
        if (!File.Exists(metadataPath))
        {
            FileInfo info = new(objectPath);
            return new LocalFileStorageMetadata(
                info.Length,
                "application/octet-stream",
                FileName: null,
                ETag: null,
                info.LastWriteTimeUtc,
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        await using FileStream input = new(
            metadataPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        LocalFileStorageMetadata? metadata = await JsonSerializer
            .DeserializeAsync<LocalFileStorageMetadata>(input, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        return metadata ?? throw new InvalidOperationException($"Local file metadata is empty for '{key.Value}'.");
    }

    private string GetObjectPath(FileStorageObjectKey key)
    {
        string[] parts = [this.rootPath, .. key.Value.Split('/')];
        string path = Path.GetFullPath(Path.Combine(parts));
        EnsurePathStaysInsideRoot(path, this.rootPath);
        return path;
    }

    private static string ResolveRootPath(string contentRootPath, string configuredRootPath)
    {
        string candidate = Path.IsPathFullyQualified(configuredRootPath)
            ? configuredRootPath
            : Path.Combine(contentRootPath, configuredRootPath);

        return Path.GetFullPath(candidate);
    }

    private static void EnsurePathStaysInsideRoot(string path, string rootPath)
    {
        string rootWithSeparator = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;

        if (!path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Resolved file storage path escaped the configured root path.");
        }
    }

    private static string MetadataPath(string objectPath) => objectPath + ".meta.json";

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static void ValidateWriteRequest(FileStorageWriteRequest request, FileManagementOptions options)
    {
        if (request.ContentLength > options.MaximumObjectBytes)
        {
            throw new InvalidOperationException("File object exceeds configured maximum length.");
        }

        if (options.AllowedContentTypes.Length > 0 &&
            !options.AllowedContentTypes.Contains(request.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("File object content type is not allowed.");
        }
    }

    private sealed record LocalFileStorageMetadata(
        long ContentLength,
        string ContentType,
        string? FileName,
        string? ETag,
        DateTimeOffset LastModifiedUtc,
        IReadOnlyDictionary<string, string> Metadata)
    {
        public static LocalFileStorageMetadata From(FileStorageWriteRequest request) =>
            new(
                request.ContentLength,
                request.ContentType,
                request.FileName,
                ETag: null,
                DateTimeOffset.UtcNow,
                request.Metadata);

        public FileStorageObjectProperties ToProperties(FileStorageObjectKey key) =>
            new(
                key,
                this.ContentLength,
                this.ContentType,
                this.FileName,
                this.ETag,
                this.LastModifiedUtc,
                this.Metadata);
    }
}
