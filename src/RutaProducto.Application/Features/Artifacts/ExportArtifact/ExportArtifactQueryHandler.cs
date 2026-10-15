using RutaProducto.Application.Abstractions.Authentication;
using RutaProducto.Application.Features.Initiatives.GetInitiative;
using MediatR;

namespace RutaProducto.Application.Features.Artifacts.ExportArtifact;

public sealed class ExportArtifactQueryHandler(
    ISender sender,
    IArtifactRepository repository,
    ICurrentUser currentUser,
    IArtifactExporter exporter) : IRequestHandler<ExportArtifactQuery, ArtifactExport?>
{
    public async Task<ArtifactExport?> Handle(ExportArtifactQuery request, CancellationToken cancellationToken)
    {
        var ownerId = await currentUser.GetRequiredIdAsync(cancellationToken);

        // The initiative query applies the same ownership and soft-delete rules as every other read.
        var initiative = await sender.Send(new GetInitiativeQuery(request.InitiativeId), cancellationToken);

        if (initiative is null)
        {
            return null;
        }

        var artifact = await repository.GetAsync(request.InitiativeId, request.Type, ownerId, cancellationToken);

        return artifact is null
            ? null
            : exporter.Export(new ArtifactExportSource(
                initiative.Name,
                artifact.Type,
                artifact.Status,
                artifact.Version,
                artifact.Content,
                artifact.CreatedAt,
                artifact.UpdatedAt));
    }
}
