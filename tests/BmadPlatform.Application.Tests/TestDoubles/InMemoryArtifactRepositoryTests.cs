using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>Guards the double itself: the use case tests are only as honest as the contract it keeps.</summary>
public sealed class InMemoryArtifactRepositoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryArtifactRepository repository = new();
    private readonly Guid initiativeId = Guid.NewGuid();

    private Artifact New(ArtifactType type) => Artifact.Create(initiativeId, "user-a", type, "# Documento", Now);

    [Fact]
    public async Task One_row_per_initiative_and_type_but_other_types_are_accepted()
    {
        await repository.AddAsync(New(ArtifactType.Brief), default);

        var error = await Assert.ThrowsAsync<ConflictException>(() => repository.AddAsync(New(ArtifactType.Brief), default));
        await repository.AddAsync(New(ArtifactType.Prd), default);

        Assert.Equal(ArtifactTexts.Conflict, error.Message);
        Assert.Equal(2, repository.Stored.Count);
    }

    [Fact]
    public async Task Loaded_copies_are_detached_and_a_stale_version_is_refused()
    {
        await repository.AddAsync(New(ArtifactType.Spec), default);
        var loaded = await repository.GetAsync(initiativeId, ArtifactType.Spec, "user-a", default);

        loaded!.Edit("# Otro", Now);

        Assert.Equal(1, repository.Stored.Single().Version);

        await repository.SaveAsync(loaded, 1, default);

        Assert.Equal(2, repository.Stored.Single().Version);
        await Assert.ThrowsAsync<ConflictException>(() => repository.SaveAsync(loaded, 1, default));
    }

    [Fact]
    public async Task Another_users_document_reads_as_missing()
    {
        await repository.AddAsync(New(ArtifactType.Spec), default);

        Assert.Null(await repository.GetAsync(initiativeId, ArtifactType.Spec, "user-b", default));
        Assert.Empty(await repository.ListAsync(initiativeId, "user-b", default));
        Assert.Single(await repository.ListAsync(initiativeId, "user-a", default));
    }
}
