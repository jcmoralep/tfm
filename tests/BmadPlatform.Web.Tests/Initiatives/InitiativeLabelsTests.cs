using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Web.Components.Initiatives;

namespace BmadPlatform.Web.Tests.Initiatives;

public sealed class InitiativeLabelsTests
{
    [Theory]
    [InlineData(InitiativeStatus.Draft, "Borrador")]
    [InlineData(InitiativeStatus.Clarifying, "Aclarando")]
    [InlineData(InitiativeStatus.Planning, "Planificando")]
    [InlineData(InitiativeStatus.ReadyToBuild, "Lista para construir")]
    public void Status_labels_are_in_Spanish(InitiativeStatus status, string expected)
    {
        Assert.Equal(expected, InitiativeLabels.Status(status));
    }

    [Theory]
    [InlineData(DepthMode.Manual, "Manual")]
    [InlineData(DepthMode.Automatic, "Automático")]
    public void Mode_labels_are_in_Spanish(DepthMode mode, string expected)
    {
        Assert.Equal(expected, InitiativeLabels.Mode(mode));
        Assert.False(string.IsNullOrWhiteSpace(InitiativeLabels.ModeDescription(mode)));
    }

    [Theory]
    [InlineData(InitiativeDepth.Small, "Pequeña")]
    [InlineData(InitiativeDepth.Standard, "Estándar")]
    [InlineData(InitiativeDepth.Large, "Grande")]
    public void Depth_labels_are_in_Spanish(InitiativeDepth depth, string expected)
    {
        Assert.Equal(expected, InitiativeLabels.Depth(depth));
    }

    [Fact]
    public void Small_yields_a_short_specification()
    {
        Assert.Equal(["Una especificación breve"], InitiativeLabels.Deliverables(InitiativeDepth.Small));
    }

    [Fact]
    public void Standard_yields_brief_and_prd()
    {
        Assert.Equal(["Brief", "PRD"], InitiativeLabels.Deliverables(InitiativeDepth.Standard));
    }

    [Fact]
    public void Large_yields_prd_architecture_and_epics_and_stories()
    {
        Assert.Equal(["PRD", "Arquitectura", "Épicas e historias"], InitiativeLabels.Deliverables(InitiativeDepth.Large));
    }

    [Theory]
    [InlineData(InitiativeDepth.Small, "Una especificación breve")]
    [InlineData(InitiativeDepth.Standard, "Brief y PRD")]
    [InlineData(InitiativeDepth.Large, "PRD, Arquitectura y Épicas e historias")]
    public void Deliverables_summary_reads_as_a_sentence(InitiativeDepth depth, string expected)
    {
        Assert.Equal(expected, InitiativeLabels.DeliverablesSummary(depth));
    }

    [Fact]
    public void Unknown_values_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InitiativeLabels.Status((InitiativeStatus)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => InitiativeLabels.Mode((DepthMode)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => InitiativeLabels.Depth((InitiativeDepth)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => InitiativeLabels.Deliverables((InitiativeDepth)99));
    }
}
