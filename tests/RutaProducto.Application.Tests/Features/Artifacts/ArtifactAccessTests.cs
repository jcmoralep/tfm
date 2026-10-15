using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Initiatives;
using RutaProducto.Application.Tests.Features.Assistant;
using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;
using RutaProducto.Domain.Initiatives;

namespace RutaProducto.Application.Tests.Features.Artifacts;

/// <summary>Rules every document use case shares: ownership, the current user and the level.</summary>
public sealed class ArtifactAccessTests
{
    private const string Generate = "generate";
    private const string Save = "save";
    private const string Regenerate = "regenerate";
    private const string Approve = "approve";
    private const string Assign = "assign";

    private readonly ArtifactTestContext context = new();

    private Task Run(string operation, Guid id, ArtifactType type = ArtifactType.Spec) => operation switch
    {
        Generate => context.Generate(id, type),
        Save => context.Save(id, type, 1, "# Otro texto"),
        Regenerate => context.Regenerate(id, type, 1),
        Approve => context.Approve(id, type, 1),
        Assign => context.Assign(id, type, 1, new QuestionAssignment("Q-1", "TI", "mañana")),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private async Task<Guid> WithSpec(string name)
    {
        var id = await context.AtClosing(InitiativeDepth.Small, name);
        await context.Generate(id, ArtifactType.Spec);

        return id;
    }

    [Theory]
    [InlineData(Generate)]
    [InlineData(Save)]
    [InlineData(Regenerate)]
    [InlineData(Approve)]
    [InlineData(Assign)]
    public async Task A_foreign_deleted_or_unknown_initiative_is_the_same_not_found_and_nothing_is_persisted(string operation)
    {
        var foreign = await WithSpec("De A");
        var deleted = await WithSpec("Borrada");
        await context.Initiatives.Delete(deleted);
        var before = (context.Artifacts.AddCount, context.Artifacts.SaveCount, context.Drafter.Requests.Count);
        var content = context.StoredArtifact(foreign, ArtifactType.Spec).Content;

        context.User.UserId = AssistantTestContext.UserB;
        var asForeign = await Assert.ThrowsAsync<NotFoundException>(() => Run(operation, foreign));
        context.User.UserId = AssistantTestContext.UserA;
        var asDeleted = await Assert.ThrowsAsync<NotFoundException>(() => Run(operation, deleted));
        var asUnknown = await Assert.ThrowsAsync<NotFoundException>(() => Run(operation, Guid.NewGuid()));

        Assert.All([asForeign, asDeleted, asUnknown], error => Assert.Equal(InitiativeTexts.NotFound, error.Message));
        Assert.Equal(before, (context.Artifacts.AddCount, context.Artifacts.SaveCount, context.Drafter.Requests.Count));
        Assert.Equal(content, context.StoredArtifact(foreign, ArtifactType.Spec).Content);
    }

    [Fact]
    public async Task Reads_of_a_foreign_deleted_or_unknown_initiative_return_nothing()
    {
        var foreign = await WithSpec("De A");
        var deleted = await WithSpec("Borrada");
        await context.Initiatives.Delete(deleted);

        context.User.UserId = AssistantTestContext.UserB;
        Assert.Null(await context.Overview(foreign));
        Assert.Null(await context.Get(foreign, ArtifactType.Spec));
        context.User.UserId = AssistantTestContext.UserA;
        Assert.Null(await context.Overview(deleted));
        Assert.Null(await context.Get(deleted, ArtifactType.Spec));
        Assert.Null(await context.Overview(Guid.NewGuid()));
        Assert.Null(await context.Get(Guid.NewGuid(), ArtifactType.Spec));
    }

    [Fact]
    public async Task Without_a_current_user_every_handler_fails_and_nothing_is_persisted()
    {
        var id = await WithSpec("App");
        var before = (context.Artifacts.AddCount, context.Artifacts.SaveCount, context.Drafter.Requests.Count);
        context.User.UserId = null;

        foreach (var operation in new[] { Generate, Save, Regenerate, Approve })
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => Run(operation, id));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Overview(id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Get(id, ArtifactType.Spec));
        Assert.Equal(before, (context.Artifacts.AddCount, context.Artifacts.SaveCount, context.Drafter.Requests.Count));
    }

    [Theory]
    [InlineData(Save)]
    [InlineData(Regenerate)]
    [InlineData(Approve)]
    [InlineData(Assign)]
    public async Task A_document_the_level_no_longer_requires_is_rejected_and_kept(string operation)
    {
        var id = await context.ClarifiedStandard();
        await context.Generate(id, ArtifactType.Brief);
        await context.Initiatives.SetInitiativeDepth(id, InitiativeDepth.Small);

        var error = await Assert.ThrowsAsync<DomainException>(() => Run(operation, id, ArtifactType.Brief));

        Assert.Equal("Este documento no corresponde al nivel de la iniciativa.", error.Message);
        var brief = context.StoredArtifact(id, ArtifactType.Brief);
        Assert.Equal((1, ArtifactStatus.Draft), (brief.Version, brief.Status));
        Assert.Equal(1, context.Artifacts.AddCount);
        Assert.Equal(0, context.Artifacts.SaveCount);
    }
}
