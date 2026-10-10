using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Infrastructure.Assistant;

/// <summary>
/// Deterministic stand-in for the real model: a pure function of the request, with no clock, randomness or I/O,
/// and nothing leaves the process. It acknowledges the last answer, then asks the next topic with an example and
/// the reason, and never praises (RF-35). The application decides what to ask; this class only words it.
/// </summary>
/// <param name="replyDelay">
/// Pause before answering, so the "typing" indicator can be seen the way it will be with the real model. The
/// application registers a short one; tests use none.
/// </param>
public sealed class FakeAssistantService(TimeSpan replyDelay = default) : IAssistantService
{
    public async Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (replyDelay > TimeSpan.Zero)
        {
            await Task.Delay(replyDelay, cancellationToken);
        }

        var topic = request.NextTopic;
        var acknowledgement = Acknowledge(request.History, topic);

        var reply = topic.Kind switch
        {
            TopicKind.DepthProposal => request.OffersLevelChoice
                ? Compose(acknowledgement, AssistantTexts.ChooseLevelPrompt, LevelsExample(), topic.Why, null)
                : ProposeDepth(request, acknowledgement),
            TopicKind.Closing => new AssistantReply(
                $"{acknowledgement}{AssistantScript.ClosingPrompt(request.Initiative.Depth ?? InitiativeDepth.Standard)} {topic.Example}",
                null),
            _ => Compose(acknowledgement, topic.Prompt, topic.Example, topic.Why, null),
        };

        return reply;
    }

    private static AssistantReply ProposeDepth(AssistantRequest request, string acknowledgement)
    {
        var suggested = SuggestDepth(request.History);
        var prompt =
            $"Según lo que me contó, le propongo el nivel {InitiativeTexts.LevelName(suggested)}. ¿Lo usamos o prefiere elegir otro?";
        var example = $"El nivel {InitiativeTexts.LevelName(suggested)} prepara {InitiativeTexts.DeliverablesInSentence(suggested)}.";

        return Compose(acknowledgement, prompt, example, request.NextTopic.Why, suggested);
    }

    private static AssistantReply Compose(string acknowledgement, string prompt, string example, string why, InitiativeDepth? suggested) =>
        new($"{acknowledgement}{prompt}\n\nPor ejemplo: {example}\n\nPor qué lo pregunto: {why}", suggested);

    // The size answer decides the suggestion; "No sé" and anything else fall back to Standard.
    private static InitiativeDepth SuggestDepth(IReadOnlyList<ConversationTurn> history) =>
        history.LastOrDefault(t => t.Role == MessageRole.User && t.TopicKey == AssistantScript.Keys.Size)?.QuickReplyKey switch
        {
            AssistantScript.ReplyKeys.SizeSmall => InitiativeDepth.Small,
            AssistantScript.ReplyKeys.SizeProduct => InitiativeDepth.Large,
            _ => InitiativeDepth.Standard,
        };

    private static string LevelsExample() =>
        string.Join(
            "; ",
            Enum.GetValues<InitiativeDepth>()
                .Select(d => $"{InitiativeTexts.LevelName(d)} prepara {InitiativeTexts.DeliverablesInSentence(d)}")) + ".";

    private static string Acknowledge(IReadOnlyList<ConversationTurn> history, AssistantTopic topic)
    {
        if (history.LastOrDefault() is not { Role: MessageRole.User } last)
        {
            return string.Empty;
        }

        if (last.Kind == AnswerKind.Unknown)
        {
            return "No pasa nada: lo dejo anotado como pregunta pendiente y seguimos. ";
        }

        if (last.QuickReplyKey == AssistantScript.ReplyKeys.ConfirmAdd)
        {
            return "Claro, escriba lo que quiera añadir. ";
        }

        // The same decision asked again after typed text: the text was kept but did not answer it.
        if (topic.Kind is TopicKind.Choice or TopicKind.DepthProposal or TopicKind.Confirmation
            && last.TopicKey == topic.Key
            && last.QuickReplyKey is null)
        {
            // Text typed right after "Quiero añadir algo" is the addition itself.
            return history.Count >= 3 && history[^3].QuickReplyKey == AssistantScript.ReplyKeys.ConfirmAdd
                ? "Anotado. "
                : "Para seguir, elija una de las opciones. ";
        }

        return "Anotado. ";
    }
}
