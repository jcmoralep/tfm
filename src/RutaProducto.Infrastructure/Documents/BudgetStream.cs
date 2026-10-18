namespace RutaProducto.Infrastructure.Documents;

/// <summary>
/// Read-only wrapper that counts the bytes really decompressed (a zip header can lie about its sizes) against a
/// budget shared by all entries, and fails with <see cref="InvalidDataException"/> as soon as it is exceeded.
/// </summary>
internal sealed class BudgetStream(Stream inner, ByteBudget budget) : Stream
{
    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

    public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private int Count(int read)
    {
        budget.Remaining -= read;

        if (budget.Remaining < 0)
        {
            throw new InvalidDataException("The document is larger than allowed once uncompressed.");
        }

        return read;
    }
}

/// <summary>Bytes still allowed; shared by every entry of one document.</summary>
internal sealed class ByteBudget(long limit)
{
    public long Remaining { get; set; } = limit;
}
