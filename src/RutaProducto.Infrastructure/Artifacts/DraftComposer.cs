using System.Text;
using System.Text.RegularExpressions;
using RutaProducto.Application.Features.Artifacts;
using RutaProducto.Application.Features.Artifacts.Markdown;
using RutaProducto.Application.Features.Assistant;
using RutaProducto.Application.Features.Assistant.Script;
using OpenQuestionsReader = RutaProducto.Application.Features.Artifacts.Markdown.OpenQuestions;

namespace RutaProducto.Infrastructure.Artifacts;

/// <summary>
/// The pieces every document layout shares: the text being written, the identifier numbering, the registers of
/// assumptions and open questions, and the rules for quoting the user's answers. One composer builds one document,
/// so every draft numbers from 1.
/// </summary>
internal sealed partial class DraftComposer
{
    private static readonly Dictionary<string, string> TopicNames = new(StringComparer.Ordinal)
    {
        [AssistantScript.Keys.Idea] = "la idea de la iniciativa",
        [AssistantScript.Keys.Users] = "quién usará la solución",
        [AssistantScript.Keys.Problem] = "el problema que resuelve",
        [AssistantScript.Keys.Success] = "cómo se medirá el éxito",
        [AssistantScript.Keys.OutOfScope] = "qué queda fuera del alcance",
        [AssistantScript.Keys.Capabilities] = "lo que se debe poder hacer",
        [AssistantScript.Keys.Constraints] = "los límites o reglas que se deben respetar",
        [AssistantScript.Keys.Priorities] = "las prioridades",
        [AssistantScript.Keys.Integrations] = "los sistemas o equipos involucrados",
        [AssistantScript.Keys.Qualities] = "las condiciones de velocidad o seguridad",
    };

    // One answer never yields more than this many items or journeys, so many tiny lines cannot outgrow the content cap.
    private const int MaxItemsPerAnswer = 20;

    // What a "nothing" quick reply ("Ninguno", "Nada por ahora") says as a sentence. It is not an item, so it has no identifier.
    private static readonly Dictionary<string, string> NoneTexts = new(StringComparer.Ordinal)
    {
        [AssistantScript.Keys.OutOfScope] = "No se indicó nada fuera del alcance por ahora.",
        [AssistantScript.Keys.Constraints] = "No se indicaron restricciones.",
        [AssistantScript.Keys.Integrations] = "No se indicaron integraciones.",
        [AssistantScript.Keys.Qualities] = "No se indicaron condiciones especiales de velocidad o seguridad.",
    };

    private readonly StringBuilder text = new();
    private readonly ItemNumberer numberer = new();
    private readonly Dictionary<string, TopicAnswer> answers;
    private readonly Dictionary<string, string> questionIds = new(StringComparer.Ordinal);
    private readonly List<(string Id, string Text)> assumptions = [];
    private readonly List<(string Id, string Text)> questions = [];
    private readonly int answerBudget;
    private readonly DraftItems? items;

    // The longest a single model item may be, so one rambling item cannot take the whole budget of a section.
    private const int MaxModelTextLength = 400;

    public DraftComposer(DraftRequest request, int answerBudget, DraftItems? items = null, int droppedStories = 0)
    {
        Request = request;
        this.answerBudget = answerBudget;
        this.items = items;
        DroppedStories = droppedStories;
        answers = request.Answers.GroupBy(a => a.TopicKey).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
    }

    public DraftRequest Request { get; }

    /// <summary>The clip of the current rung of the budget ladder; layouts that quote text read their limits from it.</summary>
    public int AnswerBudget => answerBudget;

    /// <summary>The decisions the model proposed for the Architecture; empty with no items (the fake).</summary>
    public IReadOnlyList<DecisionItem> Decisions => items?.Decisions ?? [];

    /// <summary>The epics the model proposed; empty with no items (the fake).</summary>
    public IReadOnlyList<EpicItem> Epics => items?.Epics ?? [];

    /// <summary>
    /// How many stories, counted from the end, the Epics layout leaves out: the last rung of the budget ladder. The
    /// renderer raises it until the document fits.
    /// </summary>
    public int DroppedStories { get; }

    /// <summary>The development questions the model proposed for the Architecture; empty with no items.</summary>
    public IReadOnlyList<string> ModelQuestions => items?.DevelopmentQuestions ?? [];

    /// <summary>The initiative name on one line.</summary>
    public string InitiativeName => Collapse(Request.InitiativeName);

    /// <summary>The description on one line, clipped like an answer; null when there is none.</summary>
    public string? Description =>
        string.IsNullOrWhiteSpace(Request.Description) ? null : Clip(Escape(Collapse(Request.Description)), answerBudget);

    // With the approved Brief in the request, what only the Brief states may be an answer: the person approved it.
    private bool HasBrief => items is not null && !string.IsNullOrWhiteSpace(Request.BriefMarkdown);

    public int Count(string prefix) => numberer.Count(prefix);

    public string Next(string prefix) => numberer.Next(prefix);

    public TopicAnswer? Answer(string key) => answers.GetValueOrDefault(key);

    public bool Has(string key) => Answer(key) is { IsUnknown: false };

    public void Title(string title) => text.Append("# ").Append(title).Append('\n');

    /// <summary>Writes a required heading of the document type, so the layout can never drift from the outline.</summary>
    public void Heading(string title)
    {
        var section = ArtifactOutline.Sections(Request.Type).First(s => s.Title == title);

        Raw(section.Heading);
    }

    /// <summary>A heading that is not in the outline (a subsection a layout adds), with the spacing of any heading.</summary>
    public void Raw(string heading)
    {
        // A blank line before the heading, unless one is already there.
        if (text.Length > 0 && !(text.Length >= 2 && text[^1] == '\n' && text[^2] == '\n'))
        {
            text.Append('\n');
        }

        text.Append(heading).Append("\n\n");
    }

    public void Line(string line) => text.Append(line).Append('\n');

    public void Blank() => text.Append('\n');

    /// <summary>Registers an assumption and returns it inline: <c>[ASSUMPTION: A-1] text</c>.</summary>
    public string Assumption(string assumptionText)
    {
        var id = numberer.Next(ItemPrefix.Assumption);
        assumptions.Add((id, assumptionText));

        return $"{ArtifactOutline.AssumptionMarker(id)} {assumptionText}";
    }

    /// <summary>The open question of a topic answered "No sé": registered once, numbered in document order.</summary>
    public string Question(string key)
    {
        if (questionIds.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var id = numberer.Next(ItemPrefix.Question);
        var name = TopicNames.GetValueOrDefault(key, key);
        var asked = answers.GetValueOrDefault(key)?.Question;

        questionIds[key] = id;
        questions.Add((id, asked is null ? $"Falta saber {name}." : $"Falta saber {name}: «{asked}»"));

        return id;
    }

    /// <summary>
    /// Writes one list item per line of the answer. A "No sé" answer refers to its open question and invents
    /// nothing; a topic without an answer takes an assumption; a "nothing" quick reply is one sentence with no
    /// identifier, because it states that there is nothing to list. At most 20 items are written, then a closing
    /// "y N más" line. <paramref name="format"/> builds the item (a plain
    /// bullet by default) and <paramref name="label"/> prefixes each item with a bold label.
    /// </summary>
    public void Items(string key, Func<string, string>? format = null, string? label = null)
    {
        string Wrap(string value) => label is null ? "- " + value : $"- **{label}**: {value}";

        var answer = Answer(key);

        // "No sé" and "nothing" answers keep their own rules whatever the model says; with no model items the answer is quoted.
        if (CanTake(answer) && ModelItemsOf(key, mayClaimAnswer: answer is not null || HasBrief) is { Count: > 0 } modelItems)
        {
            WriteModelItems(modelItems, format, Wrap);

            return;
        }

        if (answer is null)
        {
            Line(Wrap(Assumption($"No se recibió una respuesta sobre {TopicNames.GetValueOrDefault(key, key)}; se confirma con quien propone la iniciativa.")));
        }
        else if (answer.IsUnknown)
        {
            Line(Wrap($"Pendiente: ver {Question(key)}."));
        }
        else if (answer.IsNone && NoneTexts.TryGetValue(key, out var none))
        {
            Line(Wrap(none));
        }
        else
        {
            var lines = Lines(answer.Text, out var omitted);

            foreach (var line in lines)
            {
                Line(format is null ? Wrap(line) : format(line));
            }

            if (omitted > 0)
            {
                Line(Wrap(MoreText(omitted)));
            }
        }
    }

    /// <summary>The closing line that stands for the items of an answer that were left out.</summary>
    public static string MoreText(int omitted) => $"y {omitted} más.";

    /// <summary>
    /// One list item with every line of the answer joined by semicolons under a single bold label. Unknown and
    /// missing answers behave as in <see cref="Items"/>.
    /// </summary>
    public void JoinedItem(string key, string label)
    {
        if (CanTake(Answer(key)) && ModelItemsOf(key, mayClaimAnswer: Answer(key) is not null || HasBrief) is { Count: > 0 } modelItems)
        {
            var shown = Fit(modelItems, out var cut);
            var parts = shown.Select(Inline).ToList();

            if (cut > 0)
            {
                parts.Add(MoreText(cut));
            }

            Line($"- **{label}**: {string.Join("; ", parts)}");

            return;
        }

        if (Answer(key) is { IsUnknown: false } answer)
        {
            var lines = Lines(answer.Text, out var omitted);

            Line($"- **{label}**: {string.Join("; ", omitted > 0 ? [.. lines, MoreText(omitted)] : lines)}");

            return;
        }

        Items(key, label: label);
    }

    /// <summary>
    /// A section no topic feeds. It writes the model's items for the slot, or the fixed assumption when the model gave
    /// none (and always, with no items), so the fake's text does not change.
    /// </summary>
    public void Open(string slotId, string fallbackAssumption)
    {
        if (ModelItemsOf(slotId, mayClaimAnswer: true) is { Count: > 0 } modelItems)
        {
            WriteModelItems(modelItems, null, value => "- " + value);

            return;
        }

        Line("- " + Assumption(fallbackAssumption));
    }

    /// <summary>
    /// The model's items for a slot that has no fixed fallback, written as list items. Nothing is written, and false is
    /// returned, when the model gave none. The model can never claim an answer here: the slot is a guess or a gap.
    /// </summary>
    public bool OptionalOpen(string slotId)
    {
        if (ModelItemsOf(slotId, mayClaimAnswer: false) is not { Count: > 0 } modelItems)
        {
            return false;
        }

        WriteModelItems(modelItems, null, value => "- " + value);

        return true;
    }

    /// <summary>Registers an open question written by the model; its text is already clean. Returns its identifier.</summary>
    public string OpenQuestion(string questionText)
    {
        var id = numberer.Next(ItemPrefix.Question);
        questions.Add((id, questionText));

        return id;
    }

    /// <summary>
    /// The open questions of the approved Brief that the model did not restate: each becomes a new <c>Q-n</c> with the
    /// default owner suffix (owners are never copied). A question already registered, such as the one a "No sé"
    /// answer forces, is not repeated. Call it after every section, before the questions are written.
    /// </summary>
    public void CarryBriefQuestions()
    {
        var known = questions.Select(q => BriefFacts.Normalize(q.Text)).ToHashSet(StringComparer.Ordinal);

        foreach (var raw in items?.CarriedQuestions ?? [])
        {
            var value = ModelText(raw);

            if (value.Length > 0 && known.Add(BriefFacts.Normalize(value)))
            {
                OpenQuestion(value);
            }
        }
    }

    // A "No sé" or "nothing" answer is handled by code, so the model's items for its topic are not used.
    private static bool CanTake(TopicAnswer? answer) => answer is null or { IsUnknown: false, IsNone: false };

    // The cleaned items of a slot. Without an answer from the person, nothing can be "answer": it is a guess.
    private List<DraftItem> ModelItemsOf(string slotId, bool mayClaimAnswer)
    {
        var cleaned = new List<DraftItem>();

        foreach (var item in items?.Of(slotId) ?? [])
        {
            var value = ModelText(item.Text);

            if (value.Length == 0)
            {
                continue;
            }

            var source = item.Source == DraftItemSource.Answer && !mayClaimAnswer ? DraftItemSource.Assumption : item.Source;
            cleaned.Add(new DraftItem(value, source));
        }

        return cleaned;
    }

    // At most 20 items, then what fits the budget (the last one clipped); cut is how many more the model gave.
    private List<DraftItem> Fit(List<DraftItem> modelItems, out int cut)
    {
        cut = Math.Max(0, modelItems.Count - MaxItemsPerAnswer);

        var kept = new List<DraftItem>();
        var used = 0;

        foreach (var item in modelItems.Take(MaxItemsPerAnswer))
        {
            if (used + item.Text.Length <= answerBudget)
            {
                kept.Add(item);
                used += item.Text.Length;

                continue;
            }

            if (answerBudget - used > 1)
            {
                kept.Add(item with { Text = Clip(item.Text, answerBudget - used) });
            }

            break;
        }

        return kept;
    }

    private void WriteModelItems(List<DraftItem> modelItems, Func<string, string>? format, Func<string, string> wrap)
    {
        var shown = Fit(modelItems, out var cut);

        foreach (var item in shown)
        {
            Line(item.Source switch
            {
                DraftItemSource.Answer => format is null ? wrap(item.Text) : format(item.Text),
                DraftItemSource.Assumption => wrap(Assumption(item.Text)),
                _ => wrap(Pending(item.Text)),
            });
        }

        if (cut > 0)
        {
            Line(wrap(MoreText(cut)));
        }
    }

    // The item as part of a sentence (the joined list): the text, a marked assumption or a reference to a question.
    private string Inline(DraftItem item) => item.Source switch
    {
        DraftItemSource.Answer => item.Text,
        DraftItemSource.Assumption => Assumption(item.Text),
        _ => Pending(item.Text),
    };

    private string Pending(string questionText) => $"Pendiente: ver {OpenQuestion(questionText)}.";

    /// <summary>
    /// Plain one-line text from what the model wrote: whitespace collapsed, Markdown and list markers removed, any
    /// identifier definition or assumption marker at the start removed (so the model cannot forge ids or index
    /// entries), a "Responsable:" or "Revisar:" label neutralized so a question cannot look assigned, a leading
    /// <c>#</c> escaped, and the whole clipped to 400 characters.
    /// </summary>
    public static string ModelText(string? raw)
    {
        var value = LinkRegex().Replace(raw ?? string.Empty, "$1");
        value = AssumptionMarkerRegex().Replace(value, " ");
        value = EmphasisRegex().Replace(value, string.Empty);
        value = Collapse(value);

        string before;

        do
        {
            before = value;
            value = ForgedIdRegex().Replace(ListMarkerRegex().Replace(value, string.Empty).Trim(), string.Empty).Trim();
        }
        while (value != before);

        value = OwnerLabelRegex().Replace(value, "$1 -");

        return Clip(Escape(value), MaxModelTextLength);
    }

    /// <summary>A list item with a functional requirement, a metric or another identifier of the prefix.</summary>
    public Func<string, string> Numbered(string prefix) => line => ArtifactOutline.Item(numberer.Next(prefix), line);

    public void AssumptionIndex()
    {
        Heading(ArtifactOutline.AssumptionIndexTitle);

        if (assumptions.Count == 0)
        {
            Line("No hay suposiciones.");

            return;
        }

        foreach (var (id, assumptionText) in assumptions)
        {
            Line(ArtifactOutline.Item(id, assumptionText));
        }
    }

    public void OpenQuestions(string title, string none, string? suffix = null)
    {
        Heading(title);

        if (questions.Count == 0)
        {
            Line(none);

            return;
        }

        foreach (var (id, questionText) in questions)
        {
            Line(ArtifactOutline.Item(id, questionText + (suffix ?? OpenQuestionsReader.DefaultSuffix)));
        }
    }

    public string Finish() => text.ToString().Trim();

    /// <summary>
    /// The answer as items: one per non-empty line, list markers removed, a line that would open a heading
    /// escaped, and the whole answer clipped to the budget with an ellipsis.
    /// </summary>
    public IReadOnlyList<string> Lines(string answer) => Lines(answer, out _);

    /// <summary>
    /// As <see cref="Lines(string)"/>, keeping at most 20 lines; <paramref name="omitted"/> is how many more lines the
    /// answer had, so the caller can say so.
    /// </summary>
    public IReadOnlyList<string> Lines(string answer, out int omitted)
    {
        var lines = answer.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n')
            .Select(raw => ListMarkerRegex().Replace(raw.Trim(), string.Empty).Trim())
            .Where(line => line.Length > 0)
            .Select(Escape)
            .ToList();

        omitted = Math.Max(0, lines.Count - MaxItemsPerAnswer);
        lines = lines.Take(MaxItemsPerAnswer).ToList();

        var kept = new List<string>();
        var used = 0;

        foreach (var line in lines)
        {
            if (used + line.Length <= answerBudget)
            {
                kept.Add(line);
                used += line.Length;

                continue;
            }

            if (answerBudget - used > 1)
            {
                kept.Add(Clip(line, answerBudget - used));
            }

            break;
        }

        return kept;
    }

    /// <summary>At most <paramref name="max"/> characters, the last one an ellipsis when something was cut.</summary>
    public static string Clip(string value, int max)
    {
        if (value.Length <= max)
        {
            return value;
        }

        var end = max - 1;

        if (end > 0 && char.IsHighSurrogate(value[end - 1]))
        {
            end--;
        }

        return value[..end].TrimEnd() + "…";
    }

    private static string Collapse(string value) => WhitespaceRegex().Replace(value, " ").Trim();

    private static string Escape(string line) => line.StartsWith('#') ? "\\" + line : line;

    [GeneratedRegex(@"^(?:[-*+•–]|\d+[.):])\s+", RegexOptions.None, 1000)]
    private static partial Regex ListMarkerRegex();

    [GeneratedRegex(@"\s+", RegexOptions.None, 1000)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\[([^\]]*)\]\([^)]*\)", RegexOptions.None, 1000)]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\[ASSUMPTION[^\]]*\]?", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex AssumptionMarkerRegex();

    [GeneratedRegex(@"\*+|_{2,}|`+|~~", RegexOptions.None, 1000)]
    private static partial Regex EmphasisRegex();

    // "FR-9", "Q - 2:" or "NFR3" at the start: the shapes the format checker reads as a definition. Also the numbering
    // the code owns in the Epics: "Epic 2", "Story 2.1", "Épica 2" and "Historia 2.1".
    [GeneratedRegex(@"^(?:(?:FR|NFR|SM|UJ|A|Q|AD)\s?-\s?\d+|(?:FR|NFR|UJ)\s?\d+|(?:Epic|Épica)\s?\d+|(?:Story|Historia)\s?\d+\.\d+)\s*:?", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex ForgedIdRegex();

    [GeneratedRegex(@"\b(Responsable|Revisar)\s*:", RegexOptions.IgnoreCase, 1000)]
    private static partial Regex OwnerLabelRegex();
}
