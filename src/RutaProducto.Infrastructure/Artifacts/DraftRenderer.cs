using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Domain.Artifacts;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// Turns a drafting request into Markdown through the layouts. Both drafters use it: the fake with no items, the
/// model-backed one with the items the model returned, so the outline, numbering and caps are the same for both.
/// </summary>
internal static class DraftRenderer
{
    // Each inserted answer is clipped to 1,500 characters. The smaller budgets are a fallback that keeps the
    // document under the content cap if the rest of the text is unusually long.
    private static readonly int[] AnswerBudgets = [1500, 1000, 600, 300, 100];

    public static string Render(DraftRequest request, DraftItems? items)
    {
        var draft = string.Empty;

        foreach (var budget in AnswerBudgets)
        {
            draft = Compose(request, budget, items);

            if (draft.Length <= Artifact.ContentMaxLength)
            {
                break;
            }
        }

        // Last rung of the ladder: the Epics drop their trailing stories one by one, so what was left out shows up as an
        // uncovered requirement with a question and the document never ends in the middle of a story.
        for (var dropped = 1; dropped <= EpicsLayout.MaxStories && draft.Length > Artifact.ContentMaxLength && request.Type == ArtifactType.EpicsAndStories; dropped++)
        {
            draft = Compose(request, AnswerBudgets[^1], items, dropped);
        }

        return draft;
    }

    private static string Compose(DraftRequest request, int answerBudget, DraftItems? items, int droppedStories = 0)
    {
        var composer = new DraftComposer(request, answerBudget, items, droppedStories);

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
            case ArtifactType.Architecture:
                ArchitectureLayout.Write(composer);
                break;
            case ArtifactType.EpicsAndStories:
                EpicsLayout.Write(composer);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Type, "Unknown document type.");
        }

        return composer.Finish();
    }
}
