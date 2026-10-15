using RutaProducto.Application.Tests.Features.Assistant;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Context;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Artifacts;

/// <summary>The attached text-like context reaches the answer-based drafts (Spec, Brief, PRD) and no other document.</summary>
public sealed class DraftContextTests
{
    private readonly ArtifactTestContext context = new();

    [Theory]
    [InlineData(InitiativeDepth.Small, ArtifactType.Spec)]
    [InlineData(InitiativeDepth.Standard, ArtifactType.Brief)]
    [InlineData(InitiativeDepth.Large, ArtifactType.Prd)]
    public async Task The_spec_brief_and_prd_requests_carry_the_text_like_excerpts(InitiativeDepth depth, ArtifactType type)
    {
        var id = await context.AtClosing(depth);
        await AddNote(id, "Nota", "los pedidos llegan por correo");
        await AddLink(id);

        await context.Generate(id, type);

        var excerpt = Assert.Single(Assert.Single(context.Drafter.Requests).Context!);
        Assert.Equal((AttachmentKind.Note, "Nota", "los pedidos llegan por correo"), (excerpt.Kind, excerpt.Title, excerpt.Text));
    }

    [Fact]
    public async Task Regenerating_reads_the_context_again()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await context.Generate(id, ArtifactType.Spec);
        await AddNote(id, "Nota nueva", "texto nuevo");

        await context.Regenerate(id, ArtifactType.Spec, expectedVersion: 1);

        Assert.Empty(context.Drafter.Requests[0].Context!);
        Assert.Equal(["Nota nueva"], context.Drafter.Requests[1].Context!.Select(e => e.Title));
    }

    [Fact]
    public async Task The_architecture_and_epics_requests_carry_no_context_even_when_the_initiative_has_some()
    {
        var id = await context.LargeApprovedThrough(ArtifactType.Prd);
        await AddNote(id, "Nota", "texto");

        await context.Generate(id, ArtifactType.Architecture);
        await context.Approve(id, ArtifactType.Architecture, 1);
        await context.Generate(id, ArtifactType.EpicsAndStories);

        Assert.Equal(
            [ArtifactType.Architecture, ArtifactType.EpicsAndStories],
            context.Drafter.Requests.TakeLast(2).Select(r => r.Type));
        Assert.All(context.Drafter.Requests.TakeLast(2), request => Assert.Null(request.Context));
    }

    [Fact]
    public async Task An_initiative_with_only_a_pdf_and_a_link_sends_no_excerpt_and_still_works()
    {
        var id = await context.AtClosing(InitiativeDepth.Small);
        await AddLink(id);
        await context.Assistant.Attachments.AddAsync(
            Attachment.CreateFile(Guid.NewGuid(), id, AssistantTestContext.UserA, "plan.pdf", "application/pdf", 10, context.Clock.GetUtcNow()),
            default);

        var view = await context.Generate(id, ArtifactType.Spec);

        Assert.True(view.Generated);
        Assert.Empty(Assert.Single(context.Drafter.Requests).Context!);
    }

    private Task AddNote(Guid id, string title, string text) =>
        context.Assistant.Attachments.AddAsync(
            Attachment.CreateNote(id, AssistantTestContext.UserA, title, text, context.Clock.GetUtcNow()),
            default);

    private Task AddLink(Guid id) =>
        context.Assistant.Attachments.AddAsync(
            Attachment.CreateLink(id, AssistantTestContext.UserA, "https://example.com/a", context.Clock.GetUtcNow()),
            default);
}
