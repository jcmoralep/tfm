using MediatR;

namespace BmadPlatform.Application.Features.Assistant.UndoLastAnswer;

/// <summary>Hides the last answer and the reaction that followed it. No reply is requested.</summary>
public sealed record UndoLastAnswerCommand(Guid InitiativeId, int ExpectedVersion) : IRequest<ConversationView>;
