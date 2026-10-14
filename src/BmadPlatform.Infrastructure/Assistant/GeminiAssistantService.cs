using System.Text.Json.Nodes;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Infrastructure.Gemini;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Infrastructure.Assistant;

/// <summary>
/// Words the asking of a Question topic with Gemini. The script keeps every decision: it picks the topic, the
/// quick replies and the level, so the model is never called for a choice, the depth proposal, the confirmation or
/// the closing, and a depth suggested by the model is never used. Blocked, malformed or rejected output falls back to
/// the script wording; quota, authentication, model and timeout failures reach the chat as
/// <see cref="AssistantUnavailableException"/> so "Reintentar" applies. Only the topic key and the reason are logged.
/// </summary>
public sealed class GeminiAssistantService(GeminiClient client, ILogger<GeminiAssistantService> logger) : IAssistantService
{
    // The script wording for non-model turns and for every fallback. No delay: the model call is the wait.
    private readonly FakeAssistantService script = new();

    public async Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        if (request.NextTopic.Kind != TopicKind.Question || request.OffersLevelChoice)
        {
            return await script.ReplyAsync(request, cancellationToken);
        }

        GeminiResponse response;

        try
        {
            response = await client.GenerateAsync(AssistantPrompt.Build(request), cancellationToken);
        }
        catch (AssistantUnavailableException failure) when (failure.Reason == AssistantUnavailableReason.Blocked)
        {
            LogFallback(request, failure.Reason.ToString());
            var wording = await script.ReplyAsync(request, cancellationToken);

            return wording with { Text = $"{wording.Text} {AssistantTexts.RephraseHint}" };
        }
        catch (AssistantUnavailableException failure) when (failure.Reason == AssistantUnavailableReason.Malformed)
        {
            LogFallback(request, failure.Reason.ToString());

            return await script.ReplyAsync(request, cancellationToken);
        }
        catch (AssistantUnavailableException failure)
        {
            throw new AssistantUnavailableException(AssistantTexts.UnavailableFor(failure.Reason), failure.Reason, failure);
        }

        var last = request.History.LastOrDefault();
        var answerPrecedes = last is { Role: MessageRole.User };

        // Code owns the acknowledgement of a "No sé" and of the opening turn; the model's words are used otherwise.
        var owned = !answerPrecedes
            ? string.Empty
            : last!.Kind == AnswerKind.Unknown ? FakeAssistantService.Acknowledge(request.History, request.NextTopic) : null;

        // Code decides whether a follow-up is allowed; a NeedsMore without that permission is rejected like malformed output.
        var check = ReplyGuard.Check(Parse(response.Json), answerPrecedes, followUpAllowed: request.FollowUpFor is not null, owned);

        if (!check.Accepted)
        {
            LogFallback(request, "Guard:" + check.Rejection);

            return await script.ReplyAsync(request, cancellationToken);
        }

        return new AssistantReply(ReplyGuard.Assemble(check.Reply!), null, check.Verdict);
    }

    private static TurnReply Parse(JsonNode json) =>
        new(
            Text(json, "verdict"),
            Text(json, "acknowledgement"),
            Text(json, "question"),
            Text(json, "example"),
            Text(json, "why"),
            json["suggestedDepth"]?.ToString());

    private static string Text(JsonNode json, string name) => json[name]?.GetValue<string>() ?? string.Empty;

    private void LogFallback(AssistantRequest request, string reason) =>
        logger.LogInformation(
            "The reply for topic {TopicKey} used the script wording ({Reason}).",
            request.NextTopic.Key,
            reason);
}
