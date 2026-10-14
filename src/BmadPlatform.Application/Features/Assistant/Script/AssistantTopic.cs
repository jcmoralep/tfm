using BmadPlatform.Domain.Assistant;

namespace BmadPlatform.Application.Features.Assistant.Script;

/// <summary>
/// One item of the script. <see cref="Example"/> and <see cref="Why"/> are written without a lead-in; the
/// assistant adds "Por ejemplo:" and "Por qué lo pregunto:" when it phrases the turn.
/// <see cref="SufficiencyHint"/> is model-facing English: the minimum an answer needs (RF-66), set on every Question topic.
/// </summary>
public sealed record AssistantTopic(
    string Key,
    TopicKind Kind,
    string Prompt,
    string Example,
    string Why,
    IReadOnlyList<QuickReply> QuickReplies,
    string? SufficiencyHint = null)
{
    /// <summary>True for the topics that count toward progress.</summary>
    public bool CountsForProgress => Kind is TopicKind.Question or TopicKind.Choice;
}
