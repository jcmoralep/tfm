using RutaProducto.Domain.Artifacts;
using RutaProducto.Domain.Common;

namespace RutaProducto.Application.Tests.Domain;

public sealed class ArtifactTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);
    private static readonly DateTimeOffset T2 = T0.AddMinutes(10);

    private static Artifact NewArtifact(string content = "# Brief\n\nTexto.") =>
        Artifact.Create(Guid.NewGuid(), "user-a", ArtifactType.Brief, content, T0);

    [Fact]
    public void First_generation_creates_version_1_draft()
    {
        var initiativeId = Guid.NewGuid();

        var artifact = Artifact.Create(initiativeId, "user-a", ArtifactType.Prd, "Contenido", T0);

        Assert.NotEqual(Guid.Empty, artifact.Id);
        Assert.Equal(initiativeId, artifact.InitiativeId);
        Assert.Equal("user-a", artifact.OwnerId);
        Assert.Equal(ArtifactType.Prd, artifact.Type);
        Assert.Equal(1, artifact.Version);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
        Assert.Equal("Contenido", artifact.Content);
        Assert.Equal(T0, artifact.CreatedAt);
        Assert.Equal(T0, artifact.UpdatedAt);
        Assert.Null(artifact.ApprovedAt);
        Assert.False(artifact.WasApproved);
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    [InlineData("\r\n\r\n")]
    [InlineData("\n \n\t")]
    public void Empty_and_whitespace_only_content_is_rejected(string content)
    {
        var create = Assert.Throws<DomainException>(() => Artifact.Create(Guid.NewGuid(), "user-a", ArtifactType.Spec, content, T0));
        var edit = Assert.Throws<DomainException>(() => NewArtifact().Edit(content, T1));

        Assert.Equal("El documento no puede estar vacío.", create.Message);
        Assert.Equal("El documento no puede estar vacío.", edit.Message);
    }

    [Fact]
    public void Exactly_20000_characters_is_accepted()
    {
        var artifact = NewArtifact(new string('a', Artifact.ContentMaxLength));

        Assert.Equal(20_000, artifact.Content.Length);
    }

    [Fact]
    public void Content_of_20001_characters_is_rejected_and_nothing_changes()
    {
        var artifact = NewArtifact();

        var create = Assert.Throws<DomainException>(() => NewArtifact(new string('a', Artifact.ContentMaxLength + 1)));
        var edit = Assert.Throws<DomainException>(() => artifact.Edit(new string('a', Artifact.ContentMaxLength + 1), T1));
        var replace = Assert.Throws<DomainException>(() => artifact.Replace(new string('a', Artifact.ContentMaxLength + 1), T1));

        Assert.Equal("El documento no puede superar los 20000 caracteres.", create.Message);
        Assert.Equal(create.Message, edit.Message);
        Assert.Equal(create.Message, replace.Message);
        Assert.Equal(1, artifact.Version);
        Assert.Equal("# Brief\n\nTexto.", artifact.Content);
    }

    [Fact]
    public void Length_is_measured_after_normalization()
    {
        var body = new string('a', 9999) + "\r\n" + new string('b', 10_000);
        var raw = "\r\n\r\n   " + body + "\r\n\r\n";

        var artifact = NewArtifact(raw);

        Assert.Equal(Artifact.ContentMaxLength, artifact.Content.Length);
        Assert.DoesNotContain('\r', artifact.Content);
        Assert.Equal(artifact.Content.Trim(), artifact.Content);
    }

    [Fact]
    public void Line_endings_are_unified()
    {
        var artifact = NewArtifact("uno\r\ndos\rtres\ncuatro");

        Assert.Equal("uno\ndos\ntres\ncuatro", artifact.Content);
    }

    [Fact]
    public void Unchanged_content_is_a_no_op()
    {
        var artifact = NewArtifact("Texto");
        artifact.Edit("Otro", T1);
        artifact.Approve(T1);

        var changed = artifact.Edit("  Otro\r\n", T2);

        Assert.False(changed);
        Assert.Equal(2, artifact.Version);
        Assert.Equal(ArtifactStatus.Approved, artifact.Status);
        Assert.Equal(T1, artifact.UpdatedAt);
        Assert.Equal(T1, artifact.ApprovedAt);
    }

    [Fact]
    public void Edit_raises_the_version_and_returns_to_draft()
    {
        var artifact = NewArtifact("Texto");
        artifact.Approve(T1);

        var changed = artifact.Edit("Texto nuevo", T2);

        Assert.True(changed);
        Assert.Equal(2, artifact.Version);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
        Assert.Null(artifact.ApprovedAt);
        Assert.Equal("Texto nuevo", artifact.Content);
        Assert.Equal(T2, artifact.UpdatedAt);
        Assert.Equal(T0, artifact.CreatedAt);
    }

    [Fact]
    public void Replace_raises_the_version_even_with_identical_content()
    {
        var artifact = NewArtifact("Texto");
        artifact.Approve(T1);

        artifact.Replace("Texto", T2);

        Assert.Equal(2, artifact.Version);
        Assert.Equal(ArtifactStatus.Draft, artifact.Status);
        Assert.Null(artifact.ApprovedAt);
        Assert.Equal(T2, artifact.UpdatedAt);
    }

    [Fact]
    public void Approve_keeps_the_version()
    {
        var artifact = NewArtifact("Texto");
        artifact.Edit("Texto 2", T1);

        artifact.Approve(T2);

        Assert.Equal(2, artifact.Version);
        Assert.Equal(ArtifactStatus.Approved, artifact.Status);
        Assert.Equal(T2, artifact.ApprovedAt);
        Assert.Equal(T2, artifact.UpdatedAt);
        Assert.True(artifact.WasApproved);
    }

    [Fact]
    public void Approving_an_approved_document_changes_nothing()
    {
        var artifact = NewArtifact();
        artifact.Approve(T1);

        artifact.Approve(T2);

        Assert.Equal(T1, artifact.ApprovedAt);
        Assert.Equal(T1, artifact.UpdatedAt);
        Assert.Equal(1, artifact.Version);
    }

    [Fact]
    public void Version_never_falls_or_repeats_across_edits_regeneration_and_approval()
    {
        var artifact = NewArtifact("v1");
        var versions = new List<int> { artifact.Version };

        artifact.Edit("v2", T1);
        versions.Add(artifact.Version);
        artifact.Approve(T1);
        versions.Add(artifact.Version);
        artifact.Replace("v3", T2);
        versions.Add(artifact.Version);
        artifact.Edit("v4", T2);
        versions.Add(artifact.Version);

        Assert.Equal([1, 2, 2, 3, 4], versions);
    }
}
