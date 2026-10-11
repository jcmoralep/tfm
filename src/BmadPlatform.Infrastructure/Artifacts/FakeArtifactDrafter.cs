using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;

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
    // Each inserted answer is clipped to 1,500 characters. The smaller budgets are a fallback that keeps the
    // document under the content cap if the rest of the text is unusually long.
    private static readonly int[] AnswerBudgets = [1500, 1000, 600, 300, 100];

    public async Task<string> DraftAsync(DraftRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        var draft = string.Empty;

        foreach (var budget in AnswerBudgets)
        {
            draft = Compose(request, budget);

            if (draft.Length <= Artifact.ContentMaxLength)
            {
                break;
            }
        }

        return draft;
    }

    private static string Compose(DraftRequest request, int answerBudget)
    {
        var composer = new DraftComposer(request, answerBudget);

        switch (request.Type)
        {
            case ArtifactType.Brief:
                BriefLayout.Write(composer);
                break;
            case ArtifactType.Prd:
                PrdLayout.Write(composer);
                break;
            case ArtifactType.Spec:
                SpecLayout.Write(composer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Unknown document type.");
        }

        return composer.Finish();
    }
}
