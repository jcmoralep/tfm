namespace RutaProducto.Application.Features.Artifacts.Markdown;

/// <summary>The identifier kinds of a generated document, written with a hyphen and a number (FR-1, Q-2).</summary>
public static class ItemPrefix
{
    public const string FunctionalRequirement = "FR";
    public const string NonFunctionalRequirement = "NFR";
    public const string UserJourney = "UJ";
    public const string SuccessMetric = "SM";
    public const string ArchitectureDecision = "AD";
    public const string Assumption = "A";
    public const string Question = "Q";

    /// <summary>Longest first, so a scan that tries them in order reads NFR before FR.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        NonFunctionalRequirement,
        FunctionalRequirement,
        UserJourney,
        SuccessMetric,
        ArchitectureDecision,
        Assumption,
        Question,
    ];
}
