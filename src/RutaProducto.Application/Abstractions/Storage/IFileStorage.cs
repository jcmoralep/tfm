namespace RutaProducto.Application.Abstractions.Storage;

/// <summary>
/// Stores the bytes of attached files under opaque keys made by the application ("{initiative}/{attachment}" in
/// hexadecimal), never under a user-supplied name. Metadata lives in the database; bytes never do.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Writes the stream under the key and returns the bytes written. The bytes are counted while they are copied, so
    /// a stream longer than declared cannot pass <paramref name="maxBytes"/>: it throws
    /// <see cref="FileTooLargeException"/> and leaves nothing behind.
    /// </summary>
    Task<long> SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken);

    /// <summary>Opens the stored bytes for reading, or returns <c>null</c> when the key has no file.</summary>
    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Deletes the file. Deleting a valid key that has no file succeeds.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
