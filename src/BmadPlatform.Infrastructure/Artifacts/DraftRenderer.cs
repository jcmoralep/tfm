using BmadPlatform.Application.Features.Artifacts;
using BmadPlatform.Domain.Artifacts;

namespace BmadPlatform.Infrastructure.Artifacts;

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

        return draft;
    }

    private static string Compose(DraftRequest request, int answerBudget, DraftItems? items)
    {
        var composer = new DraftComposer(request, answerBudget, items);

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
