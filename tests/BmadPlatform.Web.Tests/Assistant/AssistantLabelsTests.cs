using BmadPlatform.Application.Features.Assistant;
using BmadPlatform.Application.Features.Assistant.Script;
using BmadPlatform.Domain.Assistant;
using BmadPlatform.Domain.Initiatives;
using BmadPlatform.Web.Components.Assistant;

namespace BmadPlatform.Web.Tests.Assistant;

public sealed class AssistantLabelsTests
{
    private static JourneySnapshot Journey(
        JourneyStep step,
        PhaseProgress clarify,
        PhaseProgress? plan = null,
        bool planUndefined = false,
        PhaseProgress? overall = null) =>
        new(step, null, clarify, plan, planUndefined, overall, false);

    private static ConversationView View(InitiativeStatus status, bool started, JourneySnapshot? journey = null) =>
        new(
            Guid.NewGuid(),
            "Portal de clientes",
            status,
            DepthMode.Manual,
            InitiativeDepth.Standard,
            started,
            started ? 3 : 0,
            [],
            [],
            journey ?? Journey(JourneyStep.Clarify, new PhaseProgress(2, 5), new PhaseProgress(0, 3), overall: new PhaseProgress(2, 8)),
            CanSend: true,
            CanUndo: false,
            NeedsResume: false,
            ReadOnly: status == InitiativeStatus.ReadyToBuild);

    [Theory]
    [InlineData(JourneyStep.Clarify, "Aclarar")]
    [InlineData(JourneyStep.Plan, "Planificar")]
    [InlineData(JourneyStep.ReadyToBuild, "Lista para construir")]
    public void Step_names_are_in_Spanish(JourneyStep step, string expected)
    {
        Assert.Equal(expected, AssistantLabels.StepName(step));
    }

    [Theory]
    [InlineData(MessageRole.Assistant, "Asistente:")]
    [InlineData(MessageRole.User, "Usted:")]
    public void Role_prefix_names_the_speaker(MessageRole role, string expected)
    {
        Assert.Equal(expected, AssistantLabels.RolePrefix(role));
    }

    [Theory]
    [InlineData(3, 5, "3 de 5 preguntas · Faltan 2")]
    [InlineData(4, 5, "4 de 5 preguntas · Falta 1")]
    [InlineData(5, 5, "5 de 5 preguntas · Completo")]
    [InlineData(0, 1, "0 de 1 pregunta · Falta 1")]
    public void Phase_summary_shows_progress_and_what_remains(int covered, int total, string expected)
    {
        Assert.Equal(expected, AssistantLabels.PhaseSummary(new PhaseProgress(covered, total)));
    }

    [Fact]
    public void Overall_progress_uses_the_topic_text_once_the_level_is_known()
    {
        var journey = Journey(JourneyStep.Clarify, new PhaseProgress(2, 5), new PhaseProgress(0, 3), overall: new PhaseProgress(2, 8));

        Assert.Equal("2 de 8 temas cubiertos", AssistantLabels.OverallProgress(journey));
        Assert.Equal(25, AssistantLabels.OverallPercent(journey));
    }

    [Fact]
    public void Overall_progress_counts_only_the_Aclarar_questions_while_the_level_is_unset()
    {
        var journey = Journey(JourneyStep.Clarify, new PhaseProgress(1, 7), planUndefined: true);

        Assert.Equal("1 de 7 preguntas", AssistantLabels.OverallProgress(journey));
        Assert.Equal(14, AssistantLabels.OverallPercent(journey));
    }

    [Fact]
    public void Steps_for_a_larger_level_list_three_phases_and_mark_the_current_one()
    {
        var journey = Journey(JourneyStep.Clarify, new PhaseProgress(3, 5), new PhaseProgress(0, 3), overall: new PhaseProgress(3, 8));

        var steps = AssistantLabels.Steps(journey);

        Assert.Equal(["Aclarar", "Planificar", "Lista para construir"], steps.Select(s => s.Name));
        Assert.Equal([StepState.Current, StepState.Upcoming, StepState.Upcoming], steps.Select(s => s.State));
        Assert.Equal("3 de 5 preguntas · Faltan 2", steps[0].Detail);
        Assert.Equal("0 de 3 preguntas · Faltan 3", steps[1].Detail);
        Assert.Null(steps[2].Detail);
    }

    [Fact]
    public void Steps_omit_Planificar_for_Small_and_keep_the_order()
    {
        var journey = Journey(JourneyStep.Plan, new PhaseProgress(6, 6), overall: new PhaseProgress(6, 6));

        var steps = AssistantLabels.Steps(journey);

        Assert.Equal(["Aclarar", "Lista para construir"], steps.Select(s => s.Name));
        Assert.Equal([StepState.Done, StepState.Current], steps.Select(s => s.State));
    }

    [Fact]
    public void Steps_say_that_Planificar_is_defined_when_the_level_is_chosen()
    {
        var journey = Journey(JourneyStep.Clarify, new PhaseProgress(1, 7), planUndefined: true);

        var steps = AssistantLabels.Steps(journey);

        Assert.Equal("Planificar", steps[1].Name);
        Assert.Equal("Se define al elegir el nivel", steps[1].Detail);
    }

    [Fact]
    public void Steps_mark_earlier_phases_as_done()
    {
        var journey = Journey(JourneyStep.Plan, new PhaseProgress(5, 5), new PhaseProgress(1, 3), overall: new PhaseProgress(6, 8));

        Assert.Equal(
            [StepState.Done, StepState.Current, StepState.Upcoming],
            AssistantLabels.Steps(journey).Select(s => s.State));
    }

    [Fact]
    public void Draft_card_asks_to_finish_creating_and_offers_no_action()
    {
        var card = AssistantLabels.Card(View(InitiativeStatus.Draft, started: false));

        Assert.Equal("Termine de crear la iniciativa para conversar con el asistente.", card.Text);
        Assert.Null(card.ActionLabel);
    }

    [Theory]
    [InlineData(InitiativeStatus.Clarifying)]
    [InlineData(InitiativeStatus.Planning)]
    public void Open_status_without_a_conversation_offers_to_open_the_assistant(InitiativeStatus status)
    {
        var card = AssistantLabels.Card(View(status, started: false));

        Assert.Equal("Abrir asistente", card.ActionLabel);
        Assert.False(string.IsNullOrWhiteSpace(card.Text));
    }

    [Theory]
    [InlineData(InitiativeStatus.Clarifying)]
    [InlineData(InitiativeStatus.Planning)]
    public void Open_status_with_a_conversation_shows_progress_and_continues(InitiativeStatus status)
    {
        var card = AssistantLabels.Card(View(status, started: true));

        Assert.Equal("2 de 8 temas cubiertos", card.Text);
        Assert.Equal("Continuar conversación", card.ActionLabel);
    }

    [Fact]
    public void Ready_to_build_with_a_conversation_is_read_only_history()
    {
        var card = AssistantLabels.Card(View(InitiativeStatus.ReadyToBuild, started: true));

        Assert.Equal(AssistantTexts.ReadyToBuildReadOnly, card.Text);
        Assert.Equal("Ver conversación", card.ActionLabel);
    }

    [Fact]
    public void Ready_to_build_without_a_conversation_offers_no_action()
    {
        var card = AssistantLabels.Card(View(InitiativeStatus.ReadyToBuild, started: false));

        Assert.Equal(AssistantLabels.NoConversationCardText, card.Text);
        Assert.Null(card.ActionLabel);
    }

    [Fact]
    public void The_privacy_note_is_the_full_sentence_of_the_spec()
    {
        Assert.Equal(
            "Modo de demostración: escriba solo datos de ejemplo, no información real de la empresa ni datos personales.",
            AssistantLabels.DemoNotice);
    }
}
