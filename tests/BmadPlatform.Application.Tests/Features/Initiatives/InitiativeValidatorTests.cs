using BmadPlatform.Application.Features.Initiatives.ListInitiatives;
using BmadPlatform.Application.Features.Initiatives.SaveInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.SaveInitiativeDetails;
using BmadPlatform.Application.Features.Initiatives.UpdateInitiative;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

public sealed class InitiativeValidatorTests
{
    private readonly SaveInitiativeDetailsCommandValidator details = new();

    private static SaveInitiativeDetailsCommand Details(string name, string? description = null) =>
        new(null, name, description, Advance: false);

    [Fact]
    public void Name_only_is_valid()
    {
        Assert.True(details.Validate(Details("App de pagos")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("     ")]
    public void Empty_or_whitespace_name_is_rejected(string name)
    {
        var failure = Assert.Single(details.Validate(Details(name)).Errors);

        Assert.Equal("El nombre es obligatorio.", failure.ErrorMessage);
    }

    [Fact]
    public void Name_of_120_characters_is_accepted_and_121_is_rejected()
    {
        Assert.True(details.Validate(Details(new string('a', 120))).IsValid);

        var failure = Assert.Single(details.Validate(Details(new string('a', 121))).Errors);
        Assert.Equal("El nombre no puede superar los 120 caracteres.", failure.ErrorMessage);
    }

    [Fact]
    public void Name_length_is_measured_after_trimming()
    {
        Assert.True(details.Validate(Details($"   {new string('a', 120)}   ")).IsValid);
    }

    [Fact]
    public void Description_of_1000_characters_is_accepted_and_1001_is_rejected()
    {
        Assert.True(details.Validate(Details("App", new string('d', 1000))).IsValid);

        var failure = Assert.Single(details.Validate(Details("App", new string('d', 1001))).Errors);
        Assert.Equal("La descripción no puede superar los 1000 caracteres.", failure.ErrorMessage);
    }

    [Fact]
    public void Update_command_applies_the_same_name_and_description_rules()
    {
        var validator = new UpdateInitiativeCommandValidator();

        var result = validator.Validate(
            new UpdateInitiativeCommand(Guid.NewGuid(), " ", new string('d', 1001), DepthMode.Manual, InitiativeDepth.Small));

        Assert.Equal(
            ["El nombre es obligatorio.", "La descripción no puede superar los 1000 caracteres."],
            result.Errors.Select(e => e.ErrorMessage).Order().ToArray());
    }

    [Fact]
    public void Automatic_mode_with_a_level_is_rejected()
    {
        var validator = new SaveInitiativeDepthCommandValidator();

        var failure = Assert.Single(
            validator.Validate(new SaveInitiativeDepthCommand(Guid.NewGuid(), DepthMode.Automatic, InitiativeDepth.Large, false)).Errors);

        Assert.Equal("En modo automático no se puede elegir un nivel de profundidad.", failure.ErrorMessage);
    }

    [Fact]
    public void Unknown_enum_values_are_rejected()
    {
        var validator = new SaveInitiativeDepthCommandValidator();

        var result = validator.Validate(
            new SaveInitiativeDepthCommand(Guid.NewGuid(), (DepthMode)99, (InitiativeDepth)99, false));

        Assert.Contains(result.Errors, e => e.ErrorMessage == "El modo de profundidad no es válido.");
        Assert.Contains(result.Errors, e => e.ErrorMessage == "El nivel de profundidad no es válido.");
    }

    [Fact]
    public void Manual_mode_without_a_level_is_valid_for_a_draft_step()
    {
        var validator = new SaveInitiativeDepthCommandValidator();

        Assert.True(validator.Validate(new SaveInitiativeDepthCommand(Guid.NewGuid(), DepthMode.Manual, null, false)).IsValid);
    }

    [Fact]
    public void Search_is_limited_to_120_characters()
    {
        var validator = new ListInitiativesQueryValidator();

        Assert.True(validator.Validate(new ListInitiativesQuery(new string('s', 120), null, null)).IsValid);

        var failure = Assert.Single(validator.Validate(new ListInitiativesQuery(new string('s', 121), null, null)).Errors);
        Assert.Equal("La búsqueda no puede superar los 120 caracteres.", failure.ErrorMessage);
    }

    [Fact]
    public void ToString_hides_names_descriptions_and_search_terms()
    {
        var texts = new[]
        {
            new SaveInitiativeDetailsCommand(null, "Nombre-secreto", "Descripcion-secreta", false).ToString(),
            new UpdateInitiativeCommand(Guid.NewGuid(), "Nombre-secreto", "Descripcion-secreta", DepthMode.Manual, null).ToString(),
            new ListInitiativesQuery("Nombre-secreto", null, null).ToString(),
        };

        Assert.All(texts, text => Assert.DoesNotContain("secret", text));
    }

    [Fact]
    public void Update_contract_has_no_status_member()
    {
        var members = typeof(UpdateInitiativeCommand).GetProperties().Select(p => p.PropertyType);

        Assert.DoesNotContain(typeof(InitiativeStatus), members);
        Assert.DoesNotContain(typeof(InitiativeStatus?), members);
        Assert.DoesNotContain(
            typeof(UpdateInitiativeCommand).GetProperties(),
            property => property.Name.Contains("Status", StringComparison.OrdinalIgnoreCase));
    }
}
