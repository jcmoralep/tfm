using RutaProducto.Domain.Artifacts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RutaProducto.Infrastructure.Artifacts;

internal sealed class ArtifactConfiguration : IEntityTypeConfiguration<Artifact>
{
    public const string TableName = "art_artifacts";

    // Same width as the Identity key, but without a foreign key: modules do not reference each other's tables.
    private const int OwnerIdLength = 255;

    private const int EnumLength = 20;

    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(artifact => artifact.Id);
        builder.Property(artifact => artifact.Id).ValueGeneratedNever();

        builder.Property(artifact => artifact.OwnerId)
            .HasMaxLength(OwnerIdLength)
            .IsRequired();

        builder.Property(artifact => artifact.Type)
            .HasConversion<string>()
            .HasMaxLength(EnumLength);

        builder.Property(artifact => artifact.Status)
            .HasConversion<string>()
            .HasMaxLength(EnumLength);

        // The Markdown is the single source of truth; 20,000 characters do not fit a varchar row.
        builder.Property(artifact => artifact.Content)
            .HasColumnType("longtext")
            .IsRequired();

        // One document per initiative and type; no foreign key to ini_initiatives.
        builder.HasIndex(artifact => new { artifact.InitiativeId, artifact.Type }).IsUnique();

        // Optimistic concurrency: two tabs working from different versions cannot overwrite each other.
        builder.Property(artifact => artifact.Version).IsConcurrencyToken();
    }
}
