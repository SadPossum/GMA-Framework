namespace Gma.Framework.FileManagement;

public sealed class FileStorageReadResult(
    FileStorageObjectProperties properties,
    Func<Stream, CancellationToken, Task> copyTo)
{
    public FileStorageObjectProperties Properties { get; } = properties ?? throw new ArgumentNullException(nameof(properties));
    private readonly Func<Stream, CancellationToken, Task> copyTo = copyTo ?? throw new ArgumentNullException(nameof(copyTo));

    public Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return this.copyTo(destination, cancellationToken);
    }
}
