namespace BmadPlatform.Application.Features.Assistant;

/// <summary>
/// Phrases the assistant's turn. The application owns the flow (progress, next topic, quick replies); the
/// service only turns the next topic into a message. A real implementation throws
/// <see cref="Common.Exceptions.AssistantUnavailableException"/> when it cannot answer.
/// </summary>
public interface IAssistantService
{
    Task<AssistantReply> ReplyAsync(AssistantRequest request, CancellationToken cancellationToken);
}
