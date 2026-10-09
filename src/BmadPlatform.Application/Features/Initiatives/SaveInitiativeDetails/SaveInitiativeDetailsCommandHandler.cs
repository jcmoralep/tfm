using BmadPlatform.Application.Abstractions.Authentication;
using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Domain.Initiatives;
using MediatR;

namespace BmadPlatform.Application.Features.Initiatives.SaveInitiativeDetails;

public sealed class SaveInitiativeDetailsCommandHandler(
    IInitiativeRepository repository,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : IRequestHandler<SaveInitiativeDetailsCommand, Guid>
{
    public async Task<Guid> Handle(SaveInitiativeDetailsCommand request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();

        // The step to reopen is the one the user is leaving through: forward on "Siguiente", this one on "Guardar borrador".
        var step = request.Advance ? CreationStep.Depth : CreationStep.Details;

        if (request.Id is null)
        {
            var created = Initiative.CreateDraft(ownerId, request.Name, request.Description, now);
            created.MoveToStep(step, now);
            await repository.AddAsync(created, cancellationToken);

            return created.Id;
        }

        var initiative = await repository.GetAsync(request.Id.Value, ownerId, cancellationToken)
            ?? throw new NotFoundException(InitiativeTexts.NotFound);

        initiative.Rename(request.Name, request.Description, now);
        initiative.MoveToStep(step, now);
        await repository.UpdateAsync(initiative, cancellationToken);

        return initiative.Id;
    }
}
