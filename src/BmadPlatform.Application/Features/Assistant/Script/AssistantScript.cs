using System.Globalization;
using System.Text;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Features.Assistant.Script;

/// <summary>
/// The questions the assistant asks, as data. Texts are Spanish, plain and in the "usted" register, with no
/// technical jargon. Which topics a level requires is defined here too; coverage is by key, so a topic answered
/// under one level still counts under another.
/// </summary>
public static class AssistantScript
{
    /// <summary>Topic keys.</summary>
    public static class Keys
    {
        public const string Idea = "idea";
        public const string Users = "users";
        public const string Problem = "problem";
        public const string Success = "success";
        public const string OutOfScope = "out-of-scope";
        public const string Capabilities = "capabilities";
        public const string Constraints = "constraints";
        public const string Priorities = "priorities";
        public const string Integrations = "integrations";
        public const string Qualities = "qualities";
        public const string Size = "size";
        public const string DepthProposal = "depth-proposal";
        public const string ConfirmPlanning = "confirm-planning";
        public const string Closing = "closing";
    }

    /// <summary>Quick-reply keys the application reacts to.</summary>
    public static class ReplyKeys
    {
        public const string Unknown = "unknown";
        public const string ConfirmYes = "confirm:yes";
        public const string ConfirmAdd = "confirm:add";
        public const string DepthOther = "depth:other";
        public const string SizeSmall = "small";
        public const string SizeFeature = "feature";
        public const string SizeProduct = "product";

        /// <summary>The key that picks a depth level, for example <c>depth:standard</c>.</summary>
        public static string Depth(InitiativeDepth depth) => "depth:" + depth.ToString().ToLowerInvariant();

        /// <summary>
        /// Reads a key built by <see cref="Depth"/> back into its level. False for anything else, including
        /// <see cref="DepthOther"/>, so the caller decides what an unknown key means.
        /// </summary>
        public static bool TryParseDepth(string? key, out InitiativeDepth depth)
        {
            foreach (var candidate in Enum.GetValues<InitiativeDepth>())
            {
                if (key == Depth(candidate))
                {
                    depth = candidate;

                    return true;
                }
            }

            depth = default;

            return false;
        }
    }

    public const string UnknownLabel = "No sé";

    public const string ConfirmYesLabel = "Sí, pasar a Planificar";

    public const string ConfirmAddLabel = "Quiero añadir algo";

    public const string DepthOtherLabel = "Elegir otro nivel";

    private const string DeliverablesPlaceholder = "{entregables}";

    private static readonly QuickReply Unknown = new(ReplyKeys.Unknown, UnknownLabel);

    private static readonly string[] SmallClarify =
    [
        Keys.Idea, Keys.Users, Keys.Problem, Keys.Capabilities, Keys.OutOfScope, Keys.Success,
    ];

    private static readonly string[] StandardClarify =
    [
        Keys.Idea, Keys.Users, Keys.Problem, Keys.Success, Keys.OutOfScope,
    ];

    // Automatic mode with no level yet: sizing, then the proposal. Both leave the list once a level is set.
    private static readonly string[] AutomaticClarify =
    [
        Keys.Idea, Keys.Users, Keys.Problem, Keys.Success, Keys.OutOfScope, Keys.Size, Keys.DepthProposal,
    ];

    private static readonly string[] StandardPlan = [Keys.Capabilities, Keys.Constraints, Keys.Priorities];

    private static readonly string[] LargePlan =
    [
        Keys.Capabilities, Keys.Constraints, Keys.Priorities, Keys.Integrations, Keys.Qualities,
    ];

    private static readonly Dictionary<string, AssistantTopic> TopicsByKey = BuildTopics().ToDictionary(t => t.Key);

    /// <summary>Every topic of the script.</summary>
    public static IReadOnlyCollection<AssistantTopic> Topics => TopicsByKey.Values;

    public static AssistantTopic Get(string key) =>
        TopicsByKey.TryGetValue(key, out var topic)
            ? topic
            : throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown script topic.");

    /// <summary>
    /// The Aclarar topics in order for the current mode and level. The sizing step and the depth proposal appear only
    /// while the mode is Automatic and no level is set. The confirmation is not part of the list.
    /// </summary>
    public static IReadOnlyList<string> ClarifyTopics(DepthMode? mode, InitiativeDepth? depth) => depth switch
    {
        InitiativeDepth.Small => SmallClarify,
        InitiativeDepth.Standard or InitiativeDepth.Large => StandardClarify,
        _ => mode == DepthMode.Automatic ? AutomaticClarify : StandardClarify,
    };

    /// <summary>The Planificar topics in order. Empty for Small, and null while no level is set.</summary>
    public static IReadOnlyList<string>? PlanTopics(InitiativeDepth? depth) => depth switch
    {
        InitiativeDepth.Small => [],
        InitiativeDepth.Standard => StandardPlan,
        InitiativeDepth.Large => LargePlan,
        _ => null,
    };

    /// <summary>The quick replies of the depth proposal: accept the suggested level first, or choose another one.</summary>
    public static IReadOnlyList<QuickReply> DepthProposalReplies(InitiativeDepth suggested) =>
    [
        new QuickReply(ReplyKeys.Depth(suggested), "Sí, usar el nivel " + InitiativeTexts.LevelName(suggested)),
        new QuickReply(ReplyKeys.DepthOther, DepthOtherLabel),
    ];

    /// <summary>The three levels as quick replies, shown after "Elegir otro nivel".</summary>
    public static IReadOnlyList<QuickReply> DepthLevelReplies() =>
    [
        .. Enum.GetValues<InitiativeDepth>().Select(d => new QuickReply(ReplyKeys.Depth(d), InitiativeTexts.LevelName(d))),
    ];

    /// <summary>The closing message with the deliverables of the level filled in.</summary>
    public static string ClosingPrompt(InitiativeDepth depth) =>
        Get(Keys.Closing).Prompt.Replace(DeliverablesPlaceholder, InitiativeTexts.DeliverablesInSentence(depth), StringComparison.Ordinal);

    /// <summary>
    /// True when the whole message is "no sé", "no se", "ni idea" or "no lo sé": trimmed, ignoring case, accents,
    /// surrounding punctuation and repeated spaces. Longer text containing the phrase is an ordinary answer.
    /// </summary>
    public static bool IsUnknownPhrase(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var normalized = string.Join(' ', RemoveAccents(text).ToLowerInvariant()
            .Trim()
            .Trim(',', '.', ';', ':', '!', '¡', '?', '¿', '…', '-', '"', '\'', '«', '»')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

        return normalized is "no se" or "ni idea" or "no lo se";
    }

    private static string RemoveAccents(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static IEnumerable<AssistantTopic> BuildTopics()
    {
        yield return new AssistantTopic(
            Keys.Idea,
            TopicKind.Question,
            "Cuénteme la idea con sus palabras: ¿qué quiere lograr o cambiar?",
            "«Quiero que el equipo de ventas consulte el estado de un pedido sin llamar a bodega.»",
            "Con su idea entiendo de qué trata la iniciativa y puedo hacerle las siguientes preguntas con más sentido.",
            [Unknown]);

        yield return new AssistantTopic(
            Keys.Users,
            TopicKind.Question,
            "¿Quién va a usar esto?",
            "«Las vendedoras de las tiendas y el personal de bodega.»",
            "Saber quién lo usa ayuda a que el resultado sea fácil de entender para esas personas.",
            [new QuickReply("customers", "Clientes"), new QuickReply("internal", "Personal interno"), new QuickReply("both", "Ambos"), Unknown]);

        yield return new AssistantTopic(
            Keys.Problem,
            TopicKind.Question,
            "¿Qué problema resuelve y por qué hace falta ahora?",
            "«Hoy cada consulta toma diez minutos por teléfono y los clientes se quejan de la demora.»",
            "Un problema claro permite comprobar más adelante si la solución realmente sirve.",
            [Unknown]);

        yield return new AssistantTopic(
            Keys.Success,
            TopicKind.Question,
            "¿Cómo sabremos que funcionó?",
            "«Que ninguna vendedora tenga que llamar a bodega y que bajen las quejas por demoras.»",
            "Con una meta concreta, el equipo de desarrollo sabe cuándo el trabajo está terminado.",
            [Unknown]);

        yield return new AssistantTopic(
            Keys.OutOfScope,
            TopicKind.Question,
            "¿Qué queda fuera, al menos por ahora?",
            "«No incluye pagos ni devoluciones, solo la consulta del estado del pedido.»",
            "Dejar claro lo que no se hará evita malentendidos y retrasos.",
            [new QuickReply("nothing", "Nada por ahora"), Unknown]);

        yield return new AssistantTopic(
            Keys.Capabilities,
            TopicKind.Question,
            "¿Qué debe poder hacer una persona con esto?",
            "«Buscar un pedido por su número, ver en qué estado está y recibir un aviso cuando cambie.»",
            "Las acciones concretas se convierten después en las funciones que se van a construir.",
            [Unknown]);

        yield return new AssistantTopic(
            Keys.Constraints,
            TopicKind.Question,
            "¿Hay límites o reglas que debamos respetar?",
            "«Debe funcionar desde el celular y no puede mostrar datos de otros clientes.»",
            "Los límites cambian la forma de construirlo, y es mejor conocerlos antes de empezar.",
            [new QuickReply("none", "Ninguno que yo sepa"), Unknown]);

        yield return new AssistantTopic(
            Keys.Priorities,
            TopicKind.Question,
            "Si solo pudiéramos hacer dos cosas, ¿cuáles serían?",
            "«Primero consultar el estado del pedido y, después, recibir avisos.»",
            "Así sabemos qué entregar primero si hubiera que recortar.",
            [Unknown]);

        yield return new AssistantTopic(
            Keys.Integrations,
            TopicKind.Question,
            "¿Qué otros sistemas o equipos participan?",
            "«El sistema de inventario y el equipo de logística.»",
            "Cada sistema o equipo involucrado suma trabajo de coordinación que conviene prever.",
            [new QuickReply("none", "Ninguno"), Unknown]);

        yield return new AssistantTopic(
            Keys.Qualities,
            TopicKind.Question,
            "¿Necesita algo especial de velocidad o seguridad?",
            "«Debe responder en pocos segundos y pedir inicio de sesión.»",
            "Estas condiciones influyen en cómo se diseña y en cuánto cuesta construirlo.",
            [new QuickReply("nothing", "Nada especial"), Unknown]);

        yield return new AssistantTopic(
            Keys.Size,
            TopicKind.Choice,
            "¿Qué tan grande le parece?",
            "Cambiar un texto de un reporte es un ajuste pequeño; una pantalla nueva con varios pasos es una funcionalidad completa.",
            "Su respuesta me ayuda a sugerirle cuánta documentación necesita.",
            [
                new QuickReply(ReplyKeys.SizeSmall, "Un ajuste pequeño"),
                new QuickReply(ReplyKeys.SizeFeature, "Una funcionalidad completa"),
                new QuickReply(ReplyKeys.SizeProduct, "Un producto o varias áreas"),
                Unknown,
            ]);

        // The service phrases the suggestion; the replies come from DepthProposalReplies. The three levels here
        // are what is shown again after "Elegir otro nivel".
        yield return new AssistantTopic(
            Keys.DepthProposal,
            TopicKind.DepthProposal,
            "Según lo que me contó, le propongo un nivel de documentación. ¿Lo usamos o prefiere elegir otro?",
            "El nivel Estándar prepara un Brief y un PRD.",
            "El nivel decide qué documentos se preparan y cuántas preguntas faltan.",
            DepthLevelReplies());

        yield return new AssistantTopic(
            Keys.ConfirmPlanning,
            TopicKind.Confirmation,
            "Ya tenemos lo necesario para empezar a planificar. Al pasar a Planificar el nivel queda fijo. ¿Seguimos?",
            "Si ya contó todo lo que sabe, elija «Sí, pasar a Planificar»; si le falta algo, elija «Quiero añadir algo».",
            "Usted decide cuándo pasamos a la siguiente etapa; yo no avanzo por mi cuenta.",
            [new QuickReply(ReplyKeys.ConfirmYes, ConfirmYesLabel), new QuickReply(ReplyKeys.ConfirmAdd, ConfirmAddLabel)]);

        yield return new AssistantTopic(
            Keys.Closing,
            TopicKind.Closing,
            "Listo: la iniciativa está lista para redactar " + DeliverablesPlaceholder + ". El siguiente paso es generar los documentos.",
            "Los documentos se redactan a partir de sus respuestas y usted los revisa antes de darlos por buenos.",
            "Con esto termina la etapa de preguntas.",
            []);
    }
}
