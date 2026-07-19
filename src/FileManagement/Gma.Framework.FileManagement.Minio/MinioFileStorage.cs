namespace Gma.Framework.FileManagement.Minio;

using Microsoft.Extensions.Options;
using global::Minio;
using global::Minio.DataModel.Args;
using Gma.Framework.FileManagement;

internal sealed class MinioFileStorage(
    IMinioClient client,
    IOptions<FileManagementOptions> fileManagementOptions,
    IOptions<MinioFileStorageOptions> minioOptions)
    : IFileStorage, IDisposable
{
    private const string FileNameMetadataKey = "x-amz-meta-file-name";
    private readonly SemaphoreSlim bucketLock = new(1, 1);
    private bool bucketChecked;

    public async Task<FileStorageObjectProperties> PutAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateWriteRequest(request, fileManagementOptions.Value);

        await this.EnsureBucketAsync(cancellationToken).ConfigureAwait(false);

        Dictionary<string, string> headers = request.Metadata
            .ToDictionary(item => $"x-amz-meta-{item.Key}", item => item.Value, StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            headers[FileNameMetadataKey] = request.FileName;
        }

        PutObjectArgs args = new PutObjectArgs()
            .WithBucket(minioOptions.Value.BucketName)
            .WithObject(request.Key.Value)
            .WithStreamData(request.Content)
            .WithObjectSize(request.ContentLength)
            .WithContentType(request.ContentType)
            .WithHeaders(headers);

        await client.PutObjectAsync(args, cancellationToken).ConfigureAwait(false);

        FileStorageObjectProperties? properties = await this.GetPropertiesAsync(request.Key, cancellationToken)
            .ConfigureAwait(false);

        return properties ?? new FileStorageObjectProperties(
            request.Key,
            request.ContentLength,
            request.ContentType,
            request.FileName,
            ETag: null,
            LastModifiedUtc: null,
            request.Metadata);
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

        return new FileStorageReadResult(
            properties,
            async (destination, copyCancellationToken) =>
            {
                copyCancellationToken.ThrowIfCancellationRequested();
                GetObjectArgs args = new GetObjectArgs()
                    .WithBucket(minioOptions.Value.BucketName)
                    .WithObject(key.Value)
                    .WithCallbackStream((stream, callbackCancellationToken) =>
                        stream.CopyToAsync(destination, callbackCancellationToken));

                await client.GetObjectAsync(args, copyCancellationToken).ConfigureAwait(false);
            });
    }

    public async Task<FileStorageObjectProperties?> GetPropertiesAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            StatObjectArgs args = new StatObjectArgs()
                .WithBucket(minioOptions.Value.BucketName)
                .WithObject(key.Value);
            global::Minio.DataModel.ObjectStat stat = await client.StatObjectAsync(args, cancellationToken)
                .ConfigureAwait(false);

            return new FileStorageObjectProperties(
                key,
                stat.Size,
                FileStorageMetadata.ContentTypeOrDefault(stat.ContentType),
                ExtractFileName(stat.MetaData),
                stat.ETag,
                stat.LastModified,
                ExtractMetadata(stat.MetaData));
        }
        catch (Exception exception) when (IsMissingObjectException(exception))
        {
            return null;
        }
    }

    public async Task<bool> DeleteAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default)
    {
        FileStorageObjectProperties? properties = await this.GetPropertiesAsync(key, cancellationToken).ConfigureAwait(false);
        if (properties is null)
        {
            return false;
        }

        RemoveObjectArgs args = new RemoveObjectArgs()
            .WithBucket(minioOptions.Value.BucketName)
            .WithObject(key.Value);

        await client.RemoveObjectAsync(args, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void Dispose() => this.bucketLock.Dispose();

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (this.bucketChecked)
        {
            return;
        }

        await this.bucketLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (this.bucketChecked)
            {
                return;
            }

            BucketExistsArgs existsArgs = new BucketExistsArgs().WithBucket(minioOptions.Value.BucketName);
            bool exists = await client.BucketExistsAsync(existsArgs, cancellationToken).ConfigureAwait(false);
            if (!exists)
            {
                if (!minioOptions.Value.CreateBucketIfMissing)
                {
                    throw new InvalidOperationException(
                        $"MinIO bucket '{minioOptions.Value.BucketName}' does not exist.");
                }

                MakeBucketArgs makeArgs = new MakeBucketArgs().WithBucket(minioOptions.Value.BucketName);
                await client.MakeBucketAsync(makeArgs, cancellationToken).ConfigureAwait(false);
            }

            this.bucketChecked = true;
        }
        finally
        {
            this.bucketLock.Release();
        }
    }

    private static Dictionary<string, string> ExtractMetadata(
        Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);
        foreach ((string key, string value) in metadata)
        {
            if (key.StartsWith("x-amz-meta-", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(key, FileNameMetadataKey, StringComparison.OrdinalIgnoreCase))
            {
                normalized[key["x-amz-meta-".Length..].ToLowerInvariant()] = value;
            }
        }

        return normalized;
    }

    private static string? ExtractFileName(Dictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return null;
        }

        return metadata.TryGetValue(FileNameMetadataKey, out string? fileName)
            ? FileStorageMetadata.NormalizeFileName(fileName)
            : null;
    }

    private static bool IsMissingObjectException(Exception exception) =>
        exception.GetType().Name is "ObjectNotFoundException" or "BucketNotFoundException";

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
}
