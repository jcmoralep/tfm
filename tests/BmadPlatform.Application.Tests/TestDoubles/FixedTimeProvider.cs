namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>Clock that only moves when the test advances it.</summary>
public sealed class FixedTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public FixedTimeProvider()
        : this(new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}
