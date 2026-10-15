using RutaProducto.Application.Abstractions.Storage;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>
/// In-memory storage with the contract of the real one (the byte count is enforced while copying) and switches to
/// make each operation fail. Operations are appended to the shared journal so a test can assert their order.
/// </summary>
public sealed class InMemoryFileStorage(List<string>? journal = null) : IFileStorage
{
    private readonly Dictionary<string, byte[]> files = [];

    /// <summary>The stored bytes by key; a test may remove one to simulate a lost object.</summary>
    public Dictionary<string, byte[]> Files => files;

    public Exception? FailOnSave { get; set; }

    public Exception? FailOnOpen { get; set; }

    public Exception? FailOnDelete { get; set; }

    public int DeleteCalls { get; private set; }

    public async Task<long> SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        journal?.Add("file:save");

        if (FailOnSave is not null)
        {
            throw FailOnSave;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;

        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);

            if (buffer.Length > maxBytes)
            {
                throw new FileTooLargeException(maxBytes);
            }
        }

        files[key] = buffer.ToArray();

        return buffer.Length;
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        if (FailOnOpen is not null)
        {
            throw FailOnOpen;
        }

        return Task.FromResult<Stream?>(files.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        DeleteCalls++;
        journal?.Add("file:delete");

        if (FailOnDelete is not null)
        {
            throw FailOnDelete;
        }

        files.Remove(key);

        return Task.CompletedTask;
    }
}
