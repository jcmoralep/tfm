namespace BmadPlatform.Application.Abstractions.Storage;

/// <summary>Raised by <see cref="IFileStorage.SaveAsync"/> when the stream is longer than the allowed size.</summary>
public sealed class FileTooLargeException(long maxBytes) : Exception("The file is larger than the allowed size.")
{
    public long MaxBytes { get; } = maxBytes;
}
