using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.TestDoubles;

/// <summary>
/// Assistant double: replies with a marker that names the topic, records every request, and can be told to
/// fail with the unavailable error.
/// </summary>
public sealed class ScriptedAssistantService : IAssistantService
{
    private readonly List<AssistantRequest> requests = [];

    public IReadOnlyList<AssistantRequest> Requests => requests;

    /// <summary>Number of upcoming calls that throw <see cref="AssistantUnavailableException"/>.</summary>
    public int FailuresRemaining { get; set; }

    /// <summary>The reason carried by the failing calls.</summary>
    public AssistantUnavailableReason FailureReason { get; set; } = AssistantUnavailableReason.Timeout;

    /// <summary>Returned for the depth proposal; null lets the application default to Standard.</summary>
    public InitiativeDepth? SuggestedDepth { get; set; }

    /// <summary>The verdicts of the upcoming calls, one per call; Proceed once the queue is empty.</summary>
    public Queue<ReplyVerdict> Verdicts { get; } = new();

    /// <summary>Runs at the start of the next call, once, so a test can interleave another actor.</summary>
    public Func<Task>? OnNextCall { get; set; }

    public async Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        requests.Add(request);

        if (OnNextCall is { } hook)
        {
            OnNextCall = null;
            await hook();
        }

        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;

            throw new AssistantUnavailableException(AssistantTexts.Unavailable, FailureReason);
        }

        var verdict = Verdicts.Count > 0 ? Verdicts.Dequeue() : ReplyVerdict.Proceed;

        return new AssistantReply(Marker(request.NextTopic.Key), SuggestedDepth, verdict);
    }

    /// <summary>The text this double produces for a topic.</summary>
    public static string Marker(string topicKey) => $"[asistente:{topicKey}]";
}
