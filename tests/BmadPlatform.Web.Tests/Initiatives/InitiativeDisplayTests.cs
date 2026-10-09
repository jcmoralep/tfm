using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Web.Components.Initiatives;

namespace BmadPlatform.Web.Tests.Initiatives;

public sealed class InitiativeDisplayTests
{
    [Theory]
    [InlineData(DepthMode.Manual, InitiativeDepth.Small, "Pequeña")]
    [InlineData(DepthMode.Automatic, InitiativeDepth.Large, "Grande")]
    [InlineData(DepthMode.Automatic, null, "Pendiente de sugerencia")]
    [InlineData(DepthMode.Manual, null, "Sin definir")]
    [InlineData(null, null, "Sin definir")]
    public void Depth_summary_covers_level_pending_and_undefined(DepthMode? mode, InitiativeDepth? depth, string expected)
    {
        Assert.Equal(expected, InitiativeLabels.DepthSummary(mode, depth));
    }

    [Fact]
    public void Dates_use_day_month_year_and_24_hour_time()
    {
        var value = new DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.Zero);
        var local = value.ToLocalTime();

        Assert.Equal(local.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture), InitiativeLabels.Date(value));
        Assert.Matches(@"^\d{2}/\d{2}/\d{4} \d{2}:\d{2}$", InitiativeLabels.Date(value));
    }
}
