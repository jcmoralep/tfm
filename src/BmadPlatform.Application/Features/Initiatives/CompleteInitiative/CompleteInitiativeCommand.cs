using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.CompleteInitiative;

/// <summary>Finishes the wizard: the draft becomes Clarifying.</summary>
public sealed record CompleteInitiativeCommand(Guid Id) : IRequest;
