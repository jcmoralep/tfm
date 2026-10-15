using BmadPlatform.Application.Abstractions.Storage;
using BmadPlatform.Infrastructure.Storage;

namespace BmadPlatform.Infrastructure.Tests.Storage;

public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"bmad-storage-{Guid.NewGuid():N}");
    private readonly LocalFileStorage storage;

    private static readonly string ValidKey = $"{Guid.NewGuid():N}/{Guid.NewGuid():N}";

    public LocalFileStorageTests()
    {
        storage = new LocalFileStorage(root);
    }

    [Fact]
    public async Task Saved_bytes_are_read_back_and_gone_after_delete()
    {
        byte[] bytes = [1, 2, 3, 4, 5];

        var written = await storage.SaveAsync(ValidKey, new MemoryStream(bytes), 100, CancellationToken.None);

        Assert.Equal(5, written);

        await using (var stream = await storage.OpenReadAsync(ValidKey, CancellationToken.None))
        {
            Assert.NotNull(stream);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());
        }

        await storage.DeleteAsync(ValidKey, CancellationToken.None);

        Assert.Null(await storage.OpenReadAsync(ValidKey, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_a_valid_key_without_a_file_is_silent_and_the_root_is_created_lazily()
    {
        await storage.DeleteAsync(ValidKey, CancellationToken.None);

        Assert.Null(await storage.OpenReadAsync(ValidKey, CancellationToken.None));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task A_root_with_a_trailing_separator_works_like_one_without()
    {
        var trailing = new LocalFileStorage(root + Path.DirectorySeparatorChar);

        await trailing.SaveAsync(ValidKey, new MemoryStream([7]), 10, CancellationToken.None);

        await using (var stream = await trailing.OpenReadAsync(ValidKey, CancellationToken.None))
        {
            Assert.NotNull(stream);
        }

        await trailing.DeleteAsync(ValidKey, CancellationToken.None);

        Assert.Null(await trailing.OpenReadAsync(ValidKey, CancellationToken.None));
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData("/etc/passwd")]
    [InlineData("C:\\x")]
    [InlineData("..\\x")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef/0123456789abcdef0123456789abcdef/extra")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF/0123456789abcdef0123456789abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef/0123456789abcdef0123456789abcdef\n")]
    public async Task A_key_that_is_not_the_exact_shape_is_rejected_by_every_operation_and_creates_nothing(string key)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => storage.SaveAsync(key, new MemoryStream([1]), 100, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.OpenReadAsync(key, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => storage.DeleteAsync(key, CancellationToken.None));

        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task An_oversize_write_throws_and_leaves_neither_the_file_nor_a_part_file()
    {
        await Assert.ThrowsAsync<FileTooLargeException>(
            () => storage.SaveAsync(ValidKey, new MemoryStream(new byte[101]), 100, CancellationToken.None));

        Assert.Null(await storage.OpenReadAsync(ValidKey, CancellationToken.None));
        Assert.Empty(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_valid_key_lands_below_the_root_and_nowhere_else()
    {
        await storage.SaveAsync(ValidKey, new MemoryStream([1]), 100, CancellationToken.None);

        var file = Assert.Single(Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories));

        Assert.Equal(Path.Combine(root, ValidKey.Replace('/', Path.DirectorySeparatorChar)), file);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
