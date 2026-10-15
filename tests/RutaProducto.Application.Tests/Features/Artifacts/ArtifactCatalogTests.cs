using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Artifacts;

public sealed class ArtifactCatalogTests
{
    private static ConversationAnswers Answers(bool clarify, bool plan) => new(clarify, plan, []);

    private static readonly DateTimeOffset Noon = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static ArtifactSummary Summary(ArtifactType type, ArtifactStatus status, int minutes = 0) =>
        new(type, status, 1, Noon.AddMinutes(minutes));

    // The Large set as the catalog lists it, passed explicitly so the chain tests do not depend on the catalog.
    private static readonly ArtifactSlot[] Chain =
    [
        new("PRD", ArtifactType.Prd),
        new("Arquitectura", ArtifactType.Architecture),
        new("Épicas e historias", ArtifactType.EpicsAndStories),
    ];

    private static ArtifactAvailability Chained(ArtifactType type, ArtifactStatus? prd, ArtifactStatus? architecture)
    {
        var summaries = new List<ArtifactSummary>();

        if (prd is { } p)
        {
            summaries.Add(Summary(ArtifactType.Prd, p));
        }

        if (architecture is { } a)
        {
            summaries.Add(Summary(ArtifactType.Architecture, a));
        }

        return ArtifactCatalog.Availability(type, InitiativeStatus.Planning, InitiativeDepth.Large, Answers(true, true), summaries, Chain);
    }

    [Fact]
    public void Required_set_per_level()
    {
        Assert.Equal(new ArtifactType?[] { ArtifactType.Spec }, Types(InitiativeDepth.Small));
        Assert.Equal(new ArtifactType?[] { ArtifactType.Brief, ArtifactType.Prd }, Types(InitiativeDepth.Standard));

        var large = ArtifactCatalog.RequiredFor(InitiativeDepth.Large);

        Assert.Equal(new[] { "PRD", "Arquitectura", "Épicas e historias" }, large.Select(s => s.Label));
        Assert.Equal(new ArtifactType?[] { ArtifactType.Prd, ArtifactType.Architecture, ArtifactType.EpicsAndStories }, large.Select(s => s.Type));
    }

    [Theory]
    [InlineData(InitiativeDepth.Small)]
    [InlineData(InitiativeDepth.Standard)]
    [InlineData(InitiativeDepth.Large)]
    public void Catalog_agrees_with_the_level_deliverables(InitiativeDepth depth)
    {
        var slots = ArtifactCatalog.RequiredFor(depth);
        var deliverables = InitiativeTexts.Deliverables(depth);

        Assert.Equal(deliverables.Count, slots.Count);

        for (var i = 0; i < slots.Count; i++)
        {
            Assert.Contains(slots[i].Label, deliverables[i], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Empty_depth_has_no_set()
    {
        Assert.Empty(ArtifactCatalog.RequiredFor(null));
        Assert.False(ArtifactCatalog.IsSetComplete(null, []));
        Assert.Equal(
            ArtifactAvailability.LevelNotSet,
            ArtifactCatalog.Availability(ArtifactType.Brief, InitiativeStatus.Clarifying, null, Answers(true, true), []));
    }

    [Fact]
    public void Brief_is_available_after_Aclarar_while_Clarifying()
    {
        var answers = Answers(clarify: true, plan: false);

        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Brief, InitiativeStatus.Clarifying, InitiativeDepth.Standard, answers, []));
        Assert.Equal(
            ArtifactAvailability.WaitingForClosing,
            ArtifactCatalog.Availability(ArtifactType.Prd, InitiativeStatus.Clarifying, InitiativeDepth.Standard, answers, []));
    }

    [Fact]
    public void Brief_waits_for_a_missing_Aclarar_topic()
    {
        Assert.Equal(
            ArtifactAvailability.WaitingForClarify,
            ArtifactCatalog.Availability(ArtifactType.Brief, InitiativeStatus.Clarifying, InitiativeDepth.Standard, Answers(false, false), []));
        Assert.Equal(
            ArtifactAvailability.WaitingForClarify,
            ArtifactCatalog.Availability(ArtifactType.Brief, InitiativeStatus.Clarifying, InitiativeDepth.Standard, null, []));
    }

    [Fact]
    public void Spec_is_available_only_at_the_closing()
    {
        Assert.Equal(
            ArtifactAvailability.WaitingForClosing,
            ArtifactCatalog.Availability(ArtifactType.Spec, InitiativeStatus.Clarifying, InitiativeDepth.Small, Answers(true, false), []));
        Assert.Equal(
            ArtifactAvailability.WaitingForClosing,
            ArtifactCatalog.Availability(ArtifactType.Spec, InitiativeStatus.Clarifying, InitiativeDepth.Small, Answers(true, true), []));
        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Spec, InitiativeStatus.Planning, InitiativeDepth.Small, Answers(true, true), []));
    }

    [Fact]
    public void Prd_waits_for_the_Planificar_topics()
    {
        Assert.Equal(
            ArtifactAvailability.WaitingForClosing,
            ArtifactCatalog.Availability(ArtifactType.Prd, InitiativeStatus.Planning, InitiativeDepth.Standard, Answers(true, false), []));
        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Prd, InitiativeStatus.Planning, InitiativeDepth.Standard, Answers(true, true), []));
    }

    [Fact]
    public void Large_prd_is_available_at_the_closing()
    {
        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Prd, InitiativeStatus.Planning, InitiativeDepth.Large, Answers(true, true), []));
    }

    [Theory]
    [InlineData(ArtifactType.Brief)]
    [InlineData(ArtifactType.Spec)]
    [InlineData(ArtifactType.Prd)]
    public void Draft_initiative_has_nothing_available(ArtifactType type)
    {
        Assert.Equal(
            ArtifactAvailability.InitiativeDraft,
            ArtifactCatalog.Availability(type, InitiativeStatus.Draft, InitiativeDepth.Standard, Answers(true, true), []));
    }

    [Fact]
    public void Ready_to_build_keeps_documents_available_for_regeneration()
    {
        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Spec, InitiativeStatus.ReadyToBuild, InitiativeDepth.Small, Answers(true, true), []));
        Assert.Equal(
            ArtifactAvailability.Available,
            ArtifactCatalog.Availability(ArtifactType.Brief, InitiativeStatus.ReadyToBuild, InitiativeDepth.Standard, Answers(true, true), []));
    }

    [Theory]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Small)]
    [InlineData(ArtifactType.Brief, InitiativeDepth.Large)]
    [InlineData(ArtifactType.Spec, InitiativeDepth.Standard)]
    [InlineData(ArtifactType.Prd, InitiativeDepth.Small)]
    public void Type_outside_the_level_is_not_required(ArtifactType type, InitiativeDepth depth)
    {
        Assert.Equal(
            ArtifactAvailability.NotRequired,
            ArtifactCatalog.Availability(type, InitiativeStatus.Planning, depth, Answers(true, true), []));
    }

    [Fact]
    public void Small_set_completes_with_the_approved_spec()
    {
        Assert.False(ArtifactCatalog.IsSetComplete(InitiativeDepth.Small, []));
        Assert.False(ArtifactCatalog.IsSetComplete(InitiativeDepth.Small, [Summary(ArtifactType.Spec, ArtifactStatus.Draft)]));
        Assert.True(ArtifactCatalog.IsSetComplete(InitiativeDepth.Small, [Summary(ArtifactType.Spec, ArtifactStatus.Approved)]));
    }

    [Fact]
    public void Standard_set_needs_both_documents_approved()
    {
        var approvedBrief = Summary(ArtifactType.Brief, ArtifactStatus.Approved);

        Assert.False(ArtifactCatalog.IsSetComplete(InitiativeDepth.Standard, [approvedBrief]));
        Assert.False(ArtifactCatalog.IsSetComplete(InitiativeDepth.Standard, [approvedBrief, Summary(ArtifactType.Prd, ArtifactStatus.Draft)]));
        Assert.True(ArtifactCatalog.IsSetComplete(InitiativeDepth.Standard, [approvedBrief, Summary(ArtifactType.Prd, ArtifactStatus.Approved)]));
    }

    [Fact]
    public void Orphan_documents_do_not_count_for_completion()
    {
        Assert.False(ArtifactCatalog.IsSetComplete(
            InitiativeDepth.Small,
            [Summary(ArtifactType.Brief, ArtifactStatus.Approved), Summary(ArtifactType.Prd, ArtifactStatus.Approved)]));
    }

    [Fact]
    public void A_lone_prd_does_not_complete_a_large_initiative()
    {
        Assert.False(ArtifactCatalog.IsSetComplete(InitiativeDepth.Large, [Summary(ArtifactType.Prd, ArtifactStatus.Approved)]));
    }

    [Theory]
    [InlineData(ArtifactType.Architecture, null, null, ArtifactAvailability.WaitingForPrd)]
    [InlineData(ArtifactType.Architecture, ArtifactStatus.Draft, null, ArtifactAvailability.WaitingForPrd)]
    [InlineData(ArtifactType.Architecture, ArtifactStatus.Approved, null, ArtifactAvailability.Available)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactStatus.Approved, null, ArtifactAvailability.WaitingForArchitecture)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactStatus.Approved, ArtifactStatus.Draft, ArtifactAvailability.WaitingForArchitecture)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactStatus.Draft, ArtifactStatus.Approved, ArtifactAvailability.WaitingForPrd)]
    [InlineData(ArtifactType.EpicsAndStories, ArtifactStatus.Approved, ArtifactStatus.Approved, ArtifactAvailability.Available)]
    [InlineData(ArtifactType.Prd, null, null, ArtifactAvailability.Available)]
    public void Dependent_documents_wait_for_their_approved_upstream(
        ArtifactType type, ArtifactStatus? prd, ArtifactStatus? architecture, ArtifactAvailability expected)
    {
        Assert.Equal(expected, Chained(type, prd, architecture));
    }

    [Fact]
    public void Coverage_is_checked_before_upstream()
    {
        Assert.Equal(
            ArtifactAvailability.WaitingForClosing,
            ArtifactCatalog.Availability(
                ArtifactType.Architecture,
                InitiativeStatus.Clarifying,
                InitiativeDepth.Large,
                Answers(true, false),
                [Summary(ArtifactType.Prd, ArtifactStatus.Approved)],
                Chain));
    }

    [Fact]
    public void Set_availability_is_coverage_only_and_ignores_upstream()
    {
        // The PRD is back in Draft while the dependents are approved: coverage still holds, so completion is not lost.
        Assert.True(ArtifactCatalog.IsSetAvailable(InitiativeStatus.Planning, InitiativeDepth.Standard, Answers(true, true)));
        Assert.False(ArtifactCatalog.IsSetAvailable(InitiativeStatus.Planning, InitiativeDepth.Standard, Answers(true, false)));
        Assert.Equal(
            ArtifactAvailability.WaitingForPrd,
            Chained(ArtifactType.Architecture, ArtifactStatus.Draft, ArtifactStatus.Approved));
    }

    [Theory]
    [InlineData(10, 0, ArtifactType.Prd)]
    [InlineData(0, 0, null)]
    [InlineData(-10, 0, null)]
    public void Stale_upstream_compares_the_timestamps(int prdMinutes, int architectureMinutes, ArtifactType? expected)
    {
        var summaries = new[]
        {
            Summary(ArtifactType.Prd, ArtifactStatus.Approved, prdMinutes),
            Summary(ArtifactType.Architecture, ArtifactStatus.Draft, architectureMinutes),
        };

        Assert.Equal(expected, ArtifactCatalog.StaleUpstream(ArtifactType.Architecture, summaries));
    }

    [Fact]
    public void Stale_upstream_is_absent_without_a_dependent_or_an_upstream()
    {
        Assert.Null(ArtifactCatalog.StaleUpstream(ArtifactType.Architecture, [Summary(ArtifactType.Prd, ArtifactStatus.Approved, 10)]));
        Assert.Null(ArtifactCatalog.StaleUpstream(ArtifactType.Architecture, [Summary(ArtifactType.Architecture, ArtifactStatus.Draft)]));
        Assert.Null(ArtifactCatalog.StaleUpstream(ArtifactType.Prd, [Summary(ArtifactType.Prd, ArtifactStatus.Draft, 10)]));
    }

    [Fact]
    public void Epics_report_the_first_stale_upstream()
    {
        var summaries = new[]
        {
            Summary(ArtifactType.Prd, ArtifactStatus.Approved, 5),
            Summary(ArtifactType.Architecture, ArtifactStatus.Approved, 20),
            Summary(ArtifactType.EpicsAndStories, ArtifactStatus.Draft, 10),
        };

        Assert.Equal(ArtifactType.Architecture, ArtifactCatalog.StaleUpstream(ArtifactType.EpicsAndStories, summaries));
    }

    private static ArtifactType?[] Types(InitiativeDepth depth) => [.. ArtifactCatalog.RequiredFor(depth).Select(s => s.Type)];
}
