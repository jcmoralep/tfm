using BmadPlatform.Domain.Initiatives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BmadPlatform.Infrastructure.Initiatives;

internal sealed class InitiativeConfiguration : IEntityTypeConfiguration<Initiative>
{
    public const string TableName = "ini_initiatives";

    // Case- and accent-insensitive on MySQL 8.4, so the name search does not depend on the server default.
    public const string NameCollation = "utf8mb4_0900_ai_ci";

    private const int EnumColumnLength = 20;

    // Same width as the Identity key, but without a foreign key: modules do not reference each other's tables.
    private const int OwnerIdLength = 255;

    public void Configure(EntityTypeBuilder<Initiative> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(initiative => initiative.Id);

        builder.Property(initiative => initiative.CreatedByUserId)
            .HasMaxLength(OwnerIdLength)
            .IsRequired();

        builder.Property(initiative => initiative.Name)
            .HasMaxLength(Initiative.NameMaxLength)
            .UseCollation(NameCollation)
            .IsRequired();

        builder.Property(initiative => initiative.Description)
            .HasMaxLength(Initiative.DescriptionMaxLength);

        builder.Property(initiative => initiative.Status)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        builder.Property(initiative => initiative.DepthMode)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        builder.Property(initiative => initiative.Depth)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        builder.Property(initiative => initiative.CreationStep)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        // Serves the list: one owner's initiatives, newest first.
        builder.HasIndex(initiative => new { initiative.CreatedByUserId, initiative.UpdatedAt });

        // Soft delete: deleted initiatives are invisible unless a query opts out explicitly.
        builder.HasQueryFilter(initiative => initiative.DeletedAt == null);
    }
}
