using BmadPlatform.Domain.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BmadPlatform.Infrastructure.Context;

internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public const string TableName = "ctx_attachments";

    // Same width as the Identity key, but without a foreign key: modules do not reference each other's tables.
    private const int OwnerIdLength = 255;

    private const int EnumLength = 20;

    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(attachment => attachment.Id);
        builder.Property(attachment => attachment.Id).ValueGeneratedNever();

        builder.Property(attachment => attachment.OwnerId)
            .HasMaxLength(OwnerIdLength)
            .IsRequired();

        builder.Property(attachment => attachment.Kind)
            .HasConversion<string>()
            .HasMaxLength(EnumLength);

        builder.Property(attachment => attachment.Name).HasMaxLength(AttachmentFileName.MaxLength);
        builder.Property(attachment => attachment.Url).HasMaxLength(Attachment.UrlMaxLength);
        builder.Property(attachment => attachment.Text).HasColumnType("text");
        builder.Property(attachment => attachment.ContentType).HasMaxLength(Attachment.ContentTypeMaxLength);
        builder.Property(attachment => attachment.StorageKey).HasMaxLength(Attachment.StorageKeyMaxLength);

        // The list of one initiative, oldest first; no foreign key to ini_initiatives.
        builder.HasIndex(attachment => new { attachment.InitiativeId, attachment.CreatedAt });
    }
}
