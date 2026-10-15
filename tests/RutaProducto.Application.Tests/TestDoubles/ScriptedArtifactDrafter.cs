using RutaProducto.Application.Features.Artifacts;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>
/// Drafter double: returns a small valid document that names the type and the answers it received, records every
/// request, and can be told to fail or to return fixed text.
/// </summary>
public sealed class ScriptedArtifactDrafter : IArtifactDrafter
{
    private readonly List<DraftRequest> requests = [];

    public IReadOnlyList<DraftRequest> Requests => requests;

    /// <summary>Number of upcoming calls that throw.</summary>
    public int FailuresRemaining { get; set; }

    /// <summary>What the failing calls throw; an unavailable drafter throws <c>AssistantUnavailableException</c> in production.</summary>
    public Exception Failure { get; set; } = new InvalidOperationException("The drafter is unavailable.");

    /// <summary>Returned instead of the default text when set.</summary>
    public string? Output { get; set; }

    public Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken)
    {
        requests.Add(request);

        if (FailuresRemaining > 0)
        {
            FailuresRemaining--;

            throw Failure;
        }

        return Task.FromResult(Output ?? Draft(request));
    }

    /// <summary>The text this double produces for a request: equal requests give equal text.</summary>
    public static string Draft(DraftRequest request) =>
        $"# {request.InitiativeName}\n\n- **FR-1**: {request.Type} con {request.Answers.Count} respuestas";
}
