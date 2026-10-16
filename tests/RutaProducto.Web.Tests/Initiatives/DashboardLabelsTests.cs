using RutaProducto.Domain.Initiatives;
using RutaProducto.Web.Components.Home;

namespace RutaProducto.Web.Tests.Initiatives;

public sealed class DashboardLabelsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "hace un momento")]
    [InlineData(59, "hace un momento")]
    [InlineData(60, "hace 1 minuto")]
    [InlineData(5 * 60, "hace 5 minutos")]
    [InlineData(59 * 60, "hace 59 minutos")]
    [InlineData(60 * 60, "hace 1 hora")]
    [InlineData(2 * 3600 + 1800, "hace 2 horas")]
    [InlineData(23 * 3600, "hace 23 horas")]
    [InlineData(24 * 3600, "ayer")]
    [InlineData(47 * 3600, "ayer")]
    [InlineData(48 * 3600, "hace 2 días")]
    [InlineData(3 * 86400 + 100, "hace 3 días")]
    [InlineData(7 * 86400 - 1, "hace 6 días")]
    [InlineData(7 * 86400, "09/10/2026")]
    [InlineData(-120, "hace un momento")] // a clock slightly ahead never prints a negative time
    public void Relative_time_reads_like_a_person_would_say_it(int secondsAgo, string expected)
    {
        Assert.Equal(expected, DashboardLabels.RelativeTime(Now.AddSeconds(-secondsAgo), Now));
    }

    [Theory]
    [InlineData(0, 8, 0)]
    [InlineData(1, 8, 2)]
    [InlineData(4, 8, 5)]
    [InlineData(8, 8, 10)]
    [InlineData(1, 100, 1)] // a small value never vanishes
    [InlineData(5, 0, 0)]
    public void Bar_height_is_quantized_to_ten_steps_and_only_zero_is_empty(int value, int max, int expected)
    {
        Assert.Equal(expected, DashboardLabels.BarLevel(value, max));
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(1.0, "1 día")]
    [InlineData(3.5, "3,5 días")]
    [InlineData(12.0, "12 días")]
    public void Average_days_use_a_decimal_comma_and_a_dash_when_unknown(double? days, string expected)
    {
        Assert.Equal(expected, DashboardLabels.AverageDays(days));
    }

    [Theory]
    [InlineData(InitiativeStatus.Draft, "Completar detalles", "/iniciativas/{0}/crear")]
    [InlineData(InitiativeStatus.Clarifying, "Continuar conversación", "/iniciativas/{0}/asistente")]
    [InlineData(InitiativeStatus.Planning, "Ver documentos", "/iniciativas/{0}")]
    [InlineData(InitiativeStatus.ReadyToBuild, "Ver y descargar", "/iniciativas/{0}")]
    public void Each_status_offers_one_next_step(InitiativeStatus status, string label, string hrefFormat)
    {
        var id = Guid.NewGuid();

        var step = DashboardLabels.NextStep(id, status);

        Assert.Equal(label, step.Label);
        Assert.Equal(string.Format(hrefFormat, id), step.Href);
    }
}
