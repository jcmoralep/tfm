using BmadPlatform.Domain.Assistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BmadPlatform.Infrastructure.Assistant;

internal sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public const string TableName = "asi_conversations";

    // Same width as the Identity key, but without a foreign key: modules do not reference each other's tables.
    private const int OwnerIdLength = 255;

    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(conversation => conversation.Id);
        builder.Property(conversation => conversation.Id).ValueGeneratedNever();

        builder.Property(conversation => conversation.OwnerId)
            .HasMaxLength(OwnerIdLength)
            .IsRequired();

        // One conversation per initiative; no foreign key to ini_initiatives.
        builder.HasIndex(conversation => conversation.InitiativeId).IsUnique();

        // Optimistic concurrency: two tabs working from different versions cannot overwrite each other.
        builder.Property(conversation => conversation.Version).IsConcurrencyToken();

        // Derived, read-only views over Messages: without this EF would map each one as another relationship.
        builder.Ignore(conversation => conversation.VisibleMessages);
        builder.Ignore(conversation => conversation.LastVisible);
        builder.Ignore(conversation => conversation.CanUndo);

        builder.HasMany(conversation => conversation.Messages)
            .WithOne()
            .HasForeignKey(message => message.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
