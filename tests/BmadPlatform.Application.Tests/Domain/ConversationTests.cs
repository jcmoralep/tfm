using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Common;

namespace BmadPlatform.Application.Tests.Domain;

public sealed class ConversationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset T1 = T0.AddMinutes(5);

    private static Conversation NewConversation() => Conversation.Start(Guid.NewGuid(), "user-a", T0);

    private static Conversation Opened()
    {
        var conversation = NewConversation();
        conversation.AddAssistantMessage("idea", "¿Cuál es la idea?", [], T0);

        return conversation;
    }

    private static void Ask(Conversation conversation, string topic) =>
        conversation.AddAssistantMessage(topic, $"Pregunta {topic}", [new QuickReply("unknown", "No sé")], T0);

    private static void Answer(Conversation conversation, string topic, string text = "respuesta", bool applied = false) =>
        conversation.AddUserAnswer(topic, text, AnswerKind.FreeText, null, applied, T0);

    /// <summary>Opening question, then three answers each followed by the next question.</summary>
    private static Conversation WithThreeAnswers()
    {
        var conversation = Opened();
        Answer(conversation, "idea", "A1");
        Ask(conversation, "users");
        Answer(conversation, "users", "A2");
        Ask(conversation, "problem");
        Answer(conversation, "problem", "A3");
        Ask(conversation, "success");

        return conversation;
    }

    [Fact]
    public void Start_creates_an_empty_conversation_for_the_initiative_and_owner()
    {
        var initiativeId = Guid.NewGuid();

        var conversation = Conversation.Start(initiativeId, "user-a", T0);

        Assert.NotEqual(Guid.Empty, conversation.Id);
        Assert.Equal(initiativeId, conversation.InitiativeId);
        Assert.Equal("user-a", conversation.OwnerId);
        Assert.Equal(0, conversation.Version);
        Assert.Equal(T0, conversation.CreatedAt);
        Assert.Equal(T0, conversation.UpdatedAt);
        Assert.Empty(conversation.Messages);
        Assert.Null(conversation.LastVisible);
        Assert.False(conversation.CanUndo);
    }

    [Fact]
    public void Assistant_message_keeps_its_topic_and_quick_replies()
    {
        var conversation = NewConversation();
        QuickReply[] replies = [new("both", "Ambos"), new("unknown", "No sé")];

        conversation.AddAssistantMessage("users", "¿Quién lo usa?", replies, T0);

        var message = Assert.Single(conversation.Messages);
        Assert.Equal(1, message.Sequence);
        Assert.Equal(MessageRole.Assistant, message.Role);
        Assert.Equal("users", message.TopicKey);
        Assert.Equal(replies, message.QuickReplies);
        Assert.Null(message.AnswerKind);
        Assert.Null(message.UndoneAt);
    }

    [Fact]
    public void Assistant_message_rejects_blank_or_oversized_content_and_bad_topic_key()
    {
        var conversation = NewConversation();

        Assert.Throws<DomainException>(() => conversation.AddAssistantMessage("idea", "   ", [], T0));
        Assert.Throws<DomainException>(() => conversation.AddAssistantMessage("idea", new string('a', Message.ContentMaxLength + 1), [], T0));
        Assert.Throws<DomainException>(() => conversation.AddAssistantMessage(new string('k', Message.KeyMaxLength + 1), "texto", [], T0));
        conversation.AddAssistantMessage("idea", new string('a', Message.ContentMaxLength), [], T0);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void User_answer_is_stored_trimmed_with_kind_and_key()
    {
        var conversation = Opened();

        conversation.AddUserAnswer("idea", "  mi idea  ", AnswerKind.QuickReply, "both", false, T1);

        var answer = conversation.LastVisible!;
        Assert.Equal(MessageRole.User, answer.Role);
        Assert.Equal("mi idea", answer.Content);
        Assert.Equal(AnswerKind.QuickReply, answer.AnswerKind);
        Assert.Equal("both", answer.QuickReplyKey);
        Assert.False(answer.AppliedToInitiative);
        Assert.Equal(T1, answer.CreatedAt);
    }

    [Fact]
    public void Answer_of_exactly_2000_characters_is_accepted_even_with_surrounding_spaces()
    {
        var conversation = Opened();

        conversation.AddUserAnswer("idea", $"   {new string('a', 2000)}   ", AnswerKind.FreeText, null, false, T0);

        Assert.Equal(2000, conversation.LastVisible!.Content.Length);
    }

    [Fact]
    public void Answer_of_2001_characters_is_rejected_and_nothing_is_stored()
    {
        var conversation = Opened();

        var error = Assert.Throws<DomainException>(() =>
            conversation.AddUserAnswer("idea", new string('a', 2001), AnswerKind.FreeText, null, false, T0));

        Assert.Equal("La respuesta no puede superar los 2000 caracteres.", error.Message);
        Assert.Single(conversation.Messages);
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    public void Empty_answer_is_rejected(string text)
    {
        var conversation = Opened();

        var error = Assert.Throws<DomainException>(() =>
            conversation.AddUserAnswer("idea", text, AnswerKind.FreeText, null, false, T0));

        Assert.Equal("La respuesta no puede estar vacía.", error.Message);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void Answer_kind_and_quick_reply_key_must_agree()
    {
        var conversation = Opened();

        Assert.Throws<DomainException>(() => conversation.AddUserAnswer("idea", "x", AnswerKind.QuickReply, null, false, T0));
        Assert.Throws<DomainException>(() => conversation.AddUserAnswer("idea", "x", AnswerKind.FreeText, "both", false, T0));
        Assert.Single(conversation.Messages);

        // "No sé" may come from the button (with a key) or be typed (without one).
        conversation.AddUserAnswer("idea", "No sé", AnswerKind.Unknown, "unknown", false, T0);
        Assert.Equal(AnswerKind.Unknown, conversation.LastVisible!.AnswerKind);
    }

    [Fact]
    public void Answer_is_refused_while_a_reply_is_pending()
    {
        var conversation = Opened();
        Answer(conversation, "idea");

        var error = Assert.Throws<DomainException>(() => Answer(conversation, "users"));

        Assert.Equal(
            "El asistente aún debe responder a su mensaje anterior. Vuelva a abrir la conversación para reintentar.",
            error.Message);
        Assert.Equal(2, conversation.Messages.Count);
    }

    [Fact]
    public void Answer_is_refused_on_a_conversation_without_a_question()
    {
        var conversation = NewConversation();

        Assert.Throws<DomainException>(() => Answer(conversation, "idea"));
    }

    [Fact]
    public void Every_mutator_bumps_version_and_updated_at()
    {
        var conversation = NewConversation();

        conversation.AddAssistantMessage("idea", "pregunta", [], T0);
        Assert.Equal(1, conversation.Version);

        conversation.AddUserAnswer("idea", "respuesta", AnswerKind.FreeText, null, false, T1);
        Assert.Equal(2, conversation.Version);
        Assert.Equal(T1, conversation.UpdatedAt);

        conversation.UndoLastAnswer(T1.AddMinutes(1));
        Assert.Equal(3, conversation.Version);
        Assert.Equal(T1.AddMinutes(1), conversation.UpdatedAt);
        Assert.Equal(T0, conversation.CreatedAt);
    }

    [Fact]
    public void A_refused_mutation_does_not_change_the_version()
    {
        var conversation = Opened();
        var version = conversation.Version;

        Assert.Throws<DomainException>(() => conversation.UndoLastAnswer(T1));
        Assert.Throws<DomainException>(() => Answer(conversation, "idea", "  "));

        Assert.Equal(version, conversation.Version);
    }

    [Fact]
    public void Sequence_is_monotonic_and_never_reused_after_an_undo()
    {
        var conversation = Opened();
        Answer(conversation, "idea");
        Ask(conversation, "users");

        conversation.UndoLastAnswer(T1);
        Ask(conversation, "idea-again");

        Assert.Equal([1, 2, 3, 4], conversation.Messages.Select(m => m.Sequence));
        Assert.Equal([1, 4], conversation.VisibleMessages.Select(m => m.Sequence));
    }

    [Fact]
    public void Undo_hides_the_last_answer_and_its_reaction_and_the_question_reappears()
    {
        var conversation = WithThreeAnswers();

        conversation.UndoLastAnswer(T1);

        var visible = conversation.VisibleMessages;
        Assert.Equal(["A1", "A2"], visible.Where(m => m.Role == MessageRole.User).Select(m => m.Content));
        Assert.Equal("problem", conversation.LastVisible!.TopicKey);
        Assert.Equal(MessageRole.Assistant, conversation.LastVisible.Role);
        Assert.Equal(T1, conversation.Messages.Last().UndoneAt);
        Assert.Equal(7, conversation.Messages.Count);
    }

    [Fact]
    public void Undo_after_the_first_answer_leaves_the_opening_question()
    {
        var conversation = Opened();
        Answer(conversation, "idea");
        Ask(conversation, "users");

        conversation.UndoLastAnswer(T1);

        var only = Assert.Single(conversation.VisibleMessages);
        Assert.Equal("idea", only.TopicKey);
        Assert.False(conversation.CanUndo);
    }

    [Fact]
    public void Repeated_undo_walks_back_one_answer_each_then_is_refused()
    {
        var conversation = Opened();
        Answer(conversation, "idea", "A1");
        Ask(conversation, "users");
        Answer(conversation, "users", "A2");
        Ask(conversation, "problem");

        conversation.UndoLastAnswer(T1);
        Assert.Equal("users", conversation.LastVisible!.TopicKey);
        Assert.Equal(["A1"], conversation.VisibleMessages.Where(m => m.Role == MessageRole.User).Select(m => m.Content));

        conversation.UndoLastAnswer(T1);
        Assert.Equal("idea", conversation.LastVisible!.TopicKey);

        var error = Assert.Throws<DomainException>(() => conversation.UndoLastAnswer(T1));
        Assert.Equal("No hay ninguna respuesta que deshacer.", error.Message);
    }

    [Fact]
    public void Undo_with_only_the_opening_question_is_refused()
    {
        var conversation = Opened();

        var error = Assert.Throws<DomainException>(() => conversation.UndoLastAnswer(T1));

        Assert.Equal("No hay ninguna respuesta que deshacer.", error.Message);
        Assert.Null(conversation.Messages.Single().UndoneAt);
    }

    [Fact]
    public void Undo_never_deletes_the_undone_rows()
    {
        var conversation = WithThreeAnswers();
        var before = conversation.Messages.Count;

        conversation.UndoLastAnswer(T1);

        Assert.Equal(before, conversation.Messages.Count);
        Assert.Equal(2, conversation.Messages.Count(m => m.UndoneAt is not null));
    }

    [Fact]
    public void Answer_that_changed_the_initiative_cannot_be_undone()
    {
        var conversation = Opened();
        Ask(conversation, "confirm-planning");
        conversation.AddUserAnswer("confirm-planning", "Sí, pasar a Planificar", AnswerKind.QuickReply, "confirm:yes", true, T0);

        var error = Assert.Throws<DomainException>(() => conversation.UndoLastAnswer(T1));

        Assert.Equal("No se puede deshacer una respuesta que ya cambió el estado de la iniciativa.", error.Message);
        Assert.False(conversation.CanUndo);
        Assert.All(conversation.Messages, m => Assert.Null(m.UndoneAt));
    }

    [Fact]
    public void Answer_after_the_confirmation_is_undoable_once_and_the_confirmation_is_not()
    {
        var conversation = Opened();
        Ask(conversation, "confirm-planning");
        conversation.AddUserAnswer("confirm-planning", "Sí, pasar a Planificar", AnswerKind.QuickReply, "confirm:yes", true, T0);
        Ask(conversation, "capabilities");
        Answer(conversation, "capabilities");
        Ask(conversation, "constraints");

        conversation.UndoLastAnswer(T1);
        var error = Assert.Throws<DomainException>(() => conversation.UndoLastAnswer(T1));

        Assert.Equal("No se puede deshacer una respuesta que ya cambió el estado de la iniciativa.", error.Message);
        Assert.Equal("capabilities", conversation.LastVisible!.TopicKey);
    }

    [Fact]
    public void Undo_with_a_pending_reply_hides_the_answer_without_asking_for_a_reply()
    {
        var conversation = Opened();
        Answer(conversation, "idea");

        conversation.UndoLastAnswer(T1);

        var only = Assert.Single(conversation.VisibleMessages);
        Assert.Equal(MessageRole.Assistant, only.Role);
        Assert.Equal(2, conversation.Messages.Count);
    }

    [Fact]
    public void Can_undo_reflects_the_last_visible_answer()
    {
        var conversation = Opened();
        Assert.False(conversation.CanUndo);

        Answer(conversation, "idea");
        Assert.True(conversation.CanUndo);
    }

    [Fact]
    public void Visible_messages_keep_sequence_order_after_undoing_and_asking_again()
    {
        var conversation = WithThreeAnswers();

        conversation.UndoLastAnswer(T1);
        conversation.UndoLastAnswer(T1);
        Ask(conversation, "problem");

        Assert.Equal([1, 2, 3, 8], conversation.VisibleMessages.Select(m => m.Sequence));
        Assert.Equal(8, conversation.LastVisible!.Sequence);
        Assert.Equal(2, conversation.LastVisibleOf(MessageRole.User)!.Sequence);
    }

    [Fact]
    public void The_last_visible_message_of_a_role_skips_undone_messages_and_is_null_when_none()
    {
        var conversation = Opened();

        Assert.Null(conversation.LastVisibleOf(MessageRole.User));
        Assert.Equal(1, conversation.LastVisibleOf(MessageRole.Assistant)!.Sequence);

        Answer(conversation, "idea", "A1");
        conversation.UndoLastAnswer(T1);

        Assert.Null(conversation.LastVisibleOf(MessageRole.User));
        Assert.Equal(1, conversation.LastVisible!.Sequence);
    }
}
