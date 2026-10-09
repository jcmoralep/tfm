using BmadPlatform.Application.Common.Exceptions;
using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Domain.Common;
using BmadPlatform.Web.Components.Initiatives;
using FluentValidation;
using FluentValidation.Results;

namespace BmadPlatform.Web.Tests.Initiatives;

public sealed class UserMessagesTests
{
    [Fact]
    public void Validation_failures_map_to_distinct_messages()
    {
        var exception = new ValidationException(
        [
            new ValidationFailure("Name", "El nombre es obligatorio."),
            new ValidationFailure("Name", "El nombre es obligatorio."),
            new ValidationFailure("Description", "La descripción no puede superar los 1000 caracteres."),
        ]);

        Assert.True(UserMessages.TryGet(exception, out var messages));
        Assert.Equal(
            ["El nombre es obligatorio.", "La descripción no puede superar los 1000 caracteres."],
            messages);
    }

    [Fact]
    public void Validation_exception_without_messages_is_not_mapped()
    {
        Assert.False(UserMessages.TryGet(new ValidationException([]), out var messages));
        Assert.Empty(messages);
    }

    [Fact]
    public void Not_found_maps_to_its_message()
    {
        Assert.True(UserMessages.TryGet(new NotFoundException(InitiativeTexts.NotFound), out var messages));
        Assert.Equal(["La iniciativa no existe."], messages);
    }

    [Fact]
    public void Domain_exception_maps_to_its_message()
    {
        Assert.True(UserMessages.TryGet(new DomainException("Elige un nivel de profundidad antes de finalizar."), out var messages));
        Assert.Equal(["Elige un nivel de profundidad antes de finalizar."], messages);
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(ArgumentException))]
    [InlineData(typeof(TimeoutException))]
    public void Unexpected_exceptions_are_not_mapped(Type exceptionType)
    {
        var exception = (Exception)Activator.CreateInstance(exceptionType, "boom")!;

        Assert.False(UserMessages.TryGet(exception, out var messages));
        Assert.Empty(messages);
    }
}
