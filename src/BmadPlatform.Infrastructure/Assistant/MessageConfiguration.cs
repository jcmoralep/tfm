using System.Text.Json;
using BmadPlatform.Domain.Assistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BmadPlatform.Infrastructure.Assistant;

internal sealed class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public const string TableName = "asi_messages";

    private const int EnumColumnLength = 20;

    public static readonly ValueConverter<IReadOnlyList<QuickReply>, string> QuickRepliesConverter = new(
        replies => JsonSerializer.Serialize(replies, (JsonSerializerOptions?)null),
        json => ReadQuickReplies(json));

    public static readonly ValueComparer<IReadOnlyList<QuickReply>> QuickRepliesComparer = new(
        (left, right) => ReferenceEquals(left, right) || (left != null && right != null && left.SequenceEqual(right)),
        replies => replies.Aggregate(0, (hash, reply) => HashCode.Combine(hash, reply)),
        replies => (IReadOnlyList<QuickReply>)replies.ToList());

    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();

        // Sequence is never reused (undone messages stay), so the pair identifies a line of the conversation.
        builder.HasIndex(message => new { message.ConversationId, message.Sequence }).IsUnique();

        builder.Property(message => message.Role)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        builder.Property(message => message.AnswerKind)
            .HasConversion<string>()
            .HasMaxLength(EnumColumnLength);

        builder.Property(message => message.Content)
            .HasMaxLength(Message.ContentMaxLength)
            .IsRequired();

        builder.Property(message => message.TopicKey)
            .HasMaxLength(Message.KeyMaxLength)
            .IsRequired();

        builder.Property(message => message.QuickReplyKey)
            .HasMaxLength(Message.KeyMaxLength);

        builder.Property(message => message.QuickReplies)
            .HasColumnType("json")
            .HasConversion(QuickRepliesConverter, QuickRepliesComparer);
    }

    private static IReadOnlyList<QuickReply> ReadQuickReplies(string json) =>
        JsonSerializer.Deserialize<List<QuickReply>>(json, (JsonSerializerOptions?)null) ?? [];
}
