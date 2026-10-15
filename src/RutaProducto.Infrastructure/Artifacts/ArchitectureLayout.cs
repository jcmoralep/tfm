using System.Text.RegularExpressions;
using RutaProducto.Application.Features.Artifacts.Markdown;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// The Architecture draft, shaped as the BMAD architecture spine with the sections the platform can fill honestly. Code
/// owns the headings, the <c>AD-n</c> numbering, the labels, the capability map and the stack entry in Diferido; the
/// decisions come from the model or, with none, from the Fake rule (one decision per non-functional requirement). Every
/// bind is checked against the approved PRD, so a decision is always anchored to a requirement that exists.
/// </summary>
internal static partial class ArchitectureLayout
{
    public const int MaxDecisions = 10;

    public const int MaxModelQuestions = 10;

    // Keeps the question list inside the 20-line limit of a section: the stack question, up to 8 requirement questions,
    // one summary question for the rest, and up to 10 questions of the model.
    private const int MaxRequirementQuestions = 8;

    private const string StackQuestion = "¿Qué tecnología (lenguajes, marcos y versiones) usará la solución?";

    private const string NoDecisionYet = "Ninguna todavía";

    private sealed record Decision(string Title, IReadOnlyList<string> Binds, string Prevents, string Rule);

    public static void Write(DraftComposer c)
    {
        var prd = new PrdFacts(c.Request.PrdMarkdown);
        var budget = LayoutBudget.For(c.AnswerBudget);

        // Registered first, so it is Q-1 and the first question of its section whatever else is asked.
        var stack = c.OpenQuestion(StackQuestion);

        c.Title("Arquitectura: " + c.InitiativeName);

        c.Heading("Paradigma de diseño");

        if (!c.OptionalOpen(DraftSlots.Paradigm))
        {
            c.Line("- " + c.Assumption("El paradigma de diseño lo propone el equipo de desarrollo al revisar este borrador."));
        }

        c.Heading("Invariantes heredadas");
        WriteInherited(c, prd);

        c.Heading("Invariantes y reglas");
        var decisions = WriteDecisions(c, prd, budget, out var rejected, out var omitted);

        c.Heading("Mapa de capacidades");
        WriteMap(c, prd, budget, decisions);

        c.Heading("Diferido");
        c.Line($"- **Tecnología (pila):** Los lenguajes, marcos y versiones no se deciden en este borrador: esta plataforma no conoce la pila de la empresa y la decide el equipo de desarrollo (ver {stack}).");
        c.Line("- **Esqueleto y convenciones:** La estructura del código y las convenciones de consistencia dependen de la pila elegida; el equipo de desarrollo las define después de decidirla.");
        c.OptionalOpen(DraftSlots.Deferred);

        AskAboutUncovered(c, prd, decisions);

        if (rejected > 0)
        {
            c.OpenQuestion("Una decisión del borrador tenía texto sin definir; ¿qué debe decir?");
        }

        if (omitted > 0)
        {
            c.OpenQuestion($"El borrador omitió {omitted} decisiones más allá del máximo de {MaxDecisions}; ¿qué otras decisiones de arquitectura hacen falta?");
        }

        foreach (var raw in c.ModelQuestions.Take(MaxModelQuestions))
        {
            if (!HasPlaceholder(raw) && DraftComposer.ModelText(raw) is { Length: > 0 } question)
            {
                c.OpenQuestion(question);
            }
        }

        c.OpenQuestions(ArtifactOutline.DevelopmentQuestionsTitle, "No hay preguntas para desarrollo.", OpenQuestions.DevelopmentSuffix);
        c.AssumptionIndex();
    }

    private static void WriteInherited(DraftComposer c, PrdFacts prd)
    {
        var ids = prd.Functional.Concat(prd.NonFunctional).Select(item => item.Id).ToList();

        c.Line(ids.Count == 0
            ? "- " + c.Assumption("El PRD aprobado no define requisitos con identificador; el equipo de desarrollo los confirma al revisar este borrador.")
            : "- Requisitos del PRD que esta arquitectura debe cumplir: " + string.Join(", ", ids) + ".");
        c.Line("- Lo que el PRD deja fuera sigue fuera.");
        c.Line("- Las preguntas abiertas del PRD siguen vigentes.");
    }

    // Numbers the surviving decisions AD-1..n in order and writes one block each; returns them with their identifiers.
    private static List<(string Id, Decision Decision)> WriteDecisions(DraftComposer c, PrdFacts prd, LayoutBudget budget, out int rejected, out int omitted)
    {
        var decisions = Normalize(c.Decisions, prd, budget, out rejected, out omitted);

        if (decisions.Count == 0)
        {
            decisions = Normalize(FakeDecisions(prd), prd, budget, out _, out omitted);
        }

        if (decisions.Count == 0)
        {
            c.Line("- " + c.Assumption("El PRD no define requisitos no funcionales; las decisiones de arquitectura las propone el equipo de desarrollo al revisar este borrador."));

            return [];
        }

        var written = new List<(string, Decision)>();

        foreach (var decision in decisions)
        {
            var id = c.Next(ItemPrefix.ArchitectureDecision);

            c.Raw($"### {id} — {decision.Title}");
            c.Line($"- **Vincula (binds):** {string.Join(", ", decision.Binds)}");
            c.Line($"- **Previene (prevents):** {c.Assumption(decision.Prevents)}");
            c.Line($"- **Regla (rule):** {c.Assumption(decision.Rule)}");

            written.Add((id, decision));
        }

        return written;
    }

    // One decision per non-functional requirement of the PRD; nothing is invented beyond the requirement itself.
    private static List<DecisionItem> FakeDecisions(PrdFacts prd) =>
    [
        .. prd.NonFunctional.Select(item => new DecisionItem(
            item.Text,
            [item.Id],
            $"Que {item.Id} se incumpla sin que nadie lo note al construir.",
            $"Toda decisión de diseño se revisa contra {item.Id} antes de construir.")),
    ];

    // Binds are canonicalised, filtered to the PRD, de-duplicated and sorted in PRD order; a decision with no valid
    // bind is dropped; at most MaxDecisions are kept. A text that is a placeholder is replaced by a fixed one and counted
    // in <rejected>, so the person is asked what the decision should say instead of reading "TBD". A decision with a
    // valid bind that does not fit under the cap is counted in <omitted>, so the loss is asked about, not silent.
    private static List<Decision> Normalize(IEnumerable<DecisionItem> source, PrdFacts prd, LayoutBudget budget, out int rejected, out int omitted)
    {
        var kept = new List<Decision>();
        rejected = 0;
        omitted = 0;

        foreach (var item in source)
        {
            var binds = item.Binds
                .Select(PrdFacts.CanonicalId)
                .OfType<string>()
                .Where(prd.Knows)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(prd.PositionOf)
                .ToList();

            if (binds.Count == 0)
            {
                continue;
            }

            if (kept.Count == MaxDecisions)
            {
                omitted++;

                continue;
            }

            if (HasPlaceholder(item.Title) || HasPlaceholder(item.Prevents) || HasPlaceholder(item.Rule))
            {
                rejected++;
            }

            kept.Add(new Decision(
                Fit(item.Title, budget, "Decisión sobre " + binds[0]),
                binds,
                Fit(item.Prevents, budget, "Que la solución se aleje de lo que pide el PRD."),
                Fit(item.Rule, budget, "Se revisa con el equipo de desarrollo antes de construir.")));
        }

        return kept;
    }

    private static string Fit(string? raw, LayoutBudget budget, string fallback) =>
        !HasPlaceholder(raw) && DraftComposer.ModelText(raw) is { Length: > 0 } text ? DraftComposer.Clip(text, budget.FieldClip) : fallback;

    // TBD and TODO markers, "{{token}}" and "<token>" placeholders, and "{token}" (which the BMAD spine linter flags).
    internal static bool HasPlaceholder(string? text)
    {
        try
        {
            return text is not null && PlaceholderRegex().IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }

    // A computed table: the model never writes it. The last column lists the decisions that bind the requirement.
    private static void WriteMap(DraftComposer c, PrdFacts prd, LayoutBudget budget, List<(string Id, Decision Decision)> decisions)
    {
        if (prd.Functional.Count == 0)
        {
            c.Line("El PRD no define requisitos funcionales con identificador.");

            return;
        }

        c.Line("| Requisito | Qué pide | Decisiones |");
        c.Line("| --- | --- | --- |");

        foreach (var requirement in prd.Functional)
        {
            var bound = DecisionsFor(requirement.Id, decisions);
            var what = Fit(requirement.Text, budget, requirement.Id).Replace("|", "\\|", StringComparison.Ordinal);

            c.Line($"| {requirement.Id} | {what} | {(bound.Count == 0 ? NoDecisionYet : string.Join(", ", bound))} |");
        }
    }

    private static void AskAboutUncovered(DraftComposer c, PrdFacts prd, List<(string Id, Decision Decision)> decisions)
    {
        var uncovered = prd.Functional.Where(item => DecisionsFor(item.Id, decisions).Count == 0).ToList();

        foreach (var item in uncovered.Take(MaxRequirementQuestions))
        {
            c.OpenQuestion($"¿Qué decisión de arquitectura cubre {item.Id}?");
        }

        if (uncovered.Count > MaxRequirementQuestions)
        {
            c.OpenQuestion($"Otros {uncovered.Count - MaxRequirementQuestions} requisitos funcionales tampoco tienen una decisión asociada: ¿qué decisión cubre cada uno?");
        }
    }

    private static List<string> DecisionsFor(string requirementId, List<(string Id, Decision Decision)> decisions) =>
        [.. decisions.Where(d => d.Decision.Binds.Contains(requirementId, StringComparer.Ordinal)).Select(d => d.Id)];

    [GeneratedRegex(@"\b(?:TBD|TODO|FIXME|XXX)\b|\{[^{}]*\}|<[^<>]+>", RegexOptions.None, 250)]
    private static partial Regex PlaceholderRegex();
}
