namespace BmadPlatform.Infrastructure.Artifacts;

/// <summary>
/// What one rung of the shared 20,000-character ladder allows a layout: how long a quoted field may be and how many
/// criteria a story keeps. The renderer walks the rungs by answer budget; the layouts that quote requirement text read
/// the same rung, so every document shrinks the same way.
/// </summary>
internal readonly record struct LayoutBudget(int FieldClip, int Criteria)
{
    public static LayoutBudget For(int answerBudget) => answerBudget switch
    {
        >= 1500 => new LayoutBudget(400, 3),
        >= 1000 => new LayoutBudget(250, 3),
        >= 600 => new LayoutBudget(160, 2),
        >= 300 => new LayoutBudget(100, 2),
        _ => new LayoutBudget(60, 1),
    };
}
