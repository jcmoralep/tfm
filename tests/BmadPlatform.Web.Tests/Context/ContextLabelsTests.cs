using BmadPlatform.Application.Features.Context;
using BmadPlatform.Domain.Context;
using BmadPlatform.Web.Components.Context;

namespace BmadPlatform.Web.Tests.Context;

public sealed class ContextLabelsTests
{
    [Theory]
    [InlineData(0L, "0 KB")]
    [InlineData(1L, "1 KB")]
    [InlineData(1023L, "1 KB")]
    [InlineData(1024L, "1 KB")]
    [InlineData(1536L, "2 KB")]
    [InlineData(1572864L, "1,5 MB")]
    [InlineData(2516582L, "2,4 MB")]
    [InlineData(10485760L, "10 MB")]
    [InlineData(52428800L, "50 MB")]
    public void Sizes_are_shown_in_Spanish(long bytes, string expected) =>
        Assert.Equal(expected, ContextLabels.SizeLabel(bytes));

    [Fact]
    public void A_file_row_shows_its_size_and_the_rest_only_their_kind()
    {
        var created = DateTimeOffset.UnixEpoch;
        var file = new AttachmentItem(Guid.NewGuid(), AttachmentKind.File, "a.pdf", null, null, 2048, created);
        var note = new AttachmentItem(Guid.NewGuid(), AttachmentKind.Note, "Idea", null, "x", null, created);

        Assert.Equal("Archivo · 2 KB", ContextLabels.RowDetail(file));
        Assert.Equal("Nota", ContextLabels.RowDetail(note));
    }
}
