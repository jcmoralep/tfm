using BmadPlatform.Application.Features.Artifacts;

namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// Deterministic stand-in for the real model: a pure function of the request, with no clock, randomness or I/O,
/// and nothing leaves the process. It quotes the user's own words into the BMAD sections, marks everything it
/// could not fill as an assumption and every "No sé" as an open question, and never invents facts.
/// </summary>
/// <param name="delay">
/// Pause before answering, so the loading indicator can be seen the way it will be with the real model. The
/// application registers a short one; tests use none.
/// </param>
public sealed class FakeArtifactDrafter(TimeSpan delay = default) : IArtifactDrafter
{
    public async Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return DraftRenderer.Render(request, null);
    }
}
