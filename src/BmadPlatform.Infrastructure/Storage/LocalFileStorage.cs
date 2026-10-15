using System.Text.RegularExpressions;
using BmadPlatform.Application.Abstractions.Storage;

namespace BmadPlatform.Infrastructure.Storage;

/// <summary>
/// Files on local disk below one root. A key is only accepted when it has the exact shape the application makes
/// and its full path stays below the root, so no input can reach another place on disk. Writes go to a part file
/// first and are moved into place, so a failed or cancelled write never leaves a partial file under the key.
/// </summary>
internal sealed partial class LocalFileStorage : IFileStorage
{
    private const int BufferSize = 81_920;

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly string root;

    public LocalFileStorage(string rootPath)
    {
        root = Path.GetFullPath(rootPath);
    }

    public async Task<long> SaveAsync(string key, Stream content, long maxBytes, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        var partPath = $"{path}.{Guid.NewGuid():N}.part";

        // Created on the first save, not at startup.
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        try
        {
            long written = 0;

            await using (var target = new FileStream(
                partPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
            {
                var buffer = new byte[BufferSize];
                int read;

                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    written += read;

                    if (written > maxBytes)
                    {
                        throw new FileTooLargeException(maxBytes);
                    }

                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            File.Move(partPath, path, overwrite: true);

            return written;
        }
        catch
        {
            TryDelete(partPath);

            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);

        if (!File.Exists(path))
        {
            return Task.FromResult<Stream?>(null);
        }

        try
        {
            Stream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.Asynchronous);

            return Task.FromResult<Stream?>(stream);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<Stream?>(null);
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            // File.Delete does nothing when the file does not exist, but fails when its folder does not.
            File.Delete(ResolvePath(key));
        }
        catch (DirectoryNotFoundException)
        {
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        if (!KeyShape().IsMatch(key))
        {
            throw new ArgumentException("The storage key is not valid.", nameof(key));
        }

        var path = Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));

        if (!path.StartsWith(root + Path.DirectorySeparatorChar, PathComparison))
        {
            throw new ArgumentException("The storage key is not valid.", nameof(key));
        }

        return path;
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best effort: the part file is private to this call and is never read.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    [GeneratedRegex(@"^[0-9a-f]{32}/[0-9a-f]{32}\z", RegexOptions.CultureInvariant)]
    private static partial Regex KeyShape();
}
