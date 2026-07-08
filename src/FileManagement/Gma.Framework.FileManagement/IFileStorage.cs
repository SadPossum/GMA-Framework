namespace Gma.Framework.FileManagement;

public interface IFileStorage
{
    Task<FileStorageObjectProperties> PutAsync(
        FileStorageWriteRequest request,
        CancellationToken cancellationToken = default);

    Task<FileStorageReadResult?> OpenReadAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<FileStorageObjectProperties?> GetPropertiesAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        FileStorageObjectKey key,
        CancellationToken cancellationToken = default);
}
