using RutaProducto.Infrastructure.Artifacts;

namespace RutaProducto.Infrastructure.Tests.Artifacts;

public sealed class FigureGuardTests
{
    private static bool Introduces(string item, params string[] inputs)
    {
        var known = new HashSet<string>(inputs.SelectMany(FigureGuard.Tokens));

        return FigureGuard.Introduces(item, known, inputs.Any(i => i.Contains('%', StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("Se confirma con el equipo.", false)]
    [InlineData("Disponibilidad del 99 %.", true)]
    [InlineData("Responde en menos de 3 segundos.", true)]
    [InlineData("Una tasa alta %.", true)]
    [InlineData("Ver A-5 y FR-12.", false)]
    public void An_item_with_a_figure_the_inputs_do_not_have_is_flagged(string item, bool expected) =>
        Assert.Equal(expected, Introduces(item));

    [Theory]
    [InlineData("Disponibilidad del 99 %.", "Necesitamos 99% de disponibilidad")]
    [InlineData("Hasta 1.000 pedidos.", "Son unos 1,000 pedidos")]
    [InlineData("Un margen de 2,5 días.", "Margen de 2.5 días")]
    [InlineData("Una tasa alta %.", "Meta: 10%")]
    [InlineData("Dentro de 30 días.", "Plazo de 30 días, ver A-5")]
    public void Figures_traceable_to_the_inputs_are_kept(string item, string input) =>
        Assert.False(Introduces(item, input));

    [Theory]
    [InlineData("Disponibilidad del 99 %.", "Necesitamos buena disponibilidad")]
    [InlineData("Hasta 1.000 pedidos.", "Son unos 100 pedidos")]
    [InlineData("Disponibilidad del 99 %.", "")]
    public void Figures_found_only_in_the_item_are_flagged(string item, string input) =>
        Assert.True(Introduces(item, input));

    [Fact]
    public void An_invented_assumption_becomes_one_fixed_question_per_section_and_nothing_else_changes()
    {
        var slots = new[]
        {
            new DraftSlot("constraints", "Requisitos no funcionales", "constraints", true, "g"),
            new DraftSlot("qualities", "Requisitos no funcionales", "qualities", true, "g"),
            new DraftSlot("users", "Usuario objetivo", "users", true, "g"),
        };
        var items = new DraftItems(new Dictionary<string, IReadOnlyList<DraftItem>>
        {
            ["constraints"] = [new("Disponibilidad del 99 %.", DraftItemSource.Assumption), new("Precisión del 100 %.", DraftItemSource.Assumption), new("Debe ser rápido.", DraftItemSource.Answer)],
            ["qualities"] = [new("Responde en 2 segundos.", DraftItemSource.Assumption)],
            ["users"] = [new("Unas 50 personas.", DraftItemSource.Assumption), new("Personal interno.", DraftItemSource.Assumption), new("Dato de 7.", DraftItemSource.Answer)],
        });

        var result = FigureGuard.Filter(items, slots, ["Son unas 50 personas"], out var converted);

        var question = FigureGuard.QuestionFor("Requisitos no funcionales");

        Assert.Equal(3, converted);
        Assert.Equal([question, "Debe ser rápido."], result.Of("constraints").Select(i => i.Text));
        Assert.Equal(DraftItemSource.Unknown, result.Of("constraints")[0].Source);
        Assert.Empty(result.Of("qualities"));
        Assert.Equal(items.Of("users"), result.Of("users"));
        Assert.DoesNotContain(question, char.IsDigit);
        Assert.Empty(FigureGuard.Filter(items, slots, [], out _).Of("qualities"));
    }
}
