using RutaProducto.Application.Common.Exceptions;
using RutaProducto.Application.Features.Initiatives.RecordInitiativeActivity;

namespace RutaProducto.Application.Tests.Features.Assistant;

public sealed class AssistantActivityTests
{
    private readonly AssistantTestContext context = new();

    private int Recorded() => context.Sender.Sent.OfType<RecordInitiativeActivityCommand>().Count();

    [Fact]
    public async Task Sending_a_message_notes_activity_after_the_answer_is_stored_and_not_when_it_is_refused()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.Start(id);
        var storedFirst = false;
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                storedFirst = context.StoredMessages(id).Any(m => m.Content == "mi respuesta");
            }
        };
        var before = Recorded();

        await Assert.ThrowsAsync<ConflictException>(() => context.Send(id, view.Version + 5, "mi respuesta"));
        Assert.Equal(before, Recorded());

        await context.Send(id, view.Version, "mi respuesta");

        Assert.Equal(before + 1, Recorded());
        Assert.Equal(id, context.Sender.Sent.OfType<RecordInitiativeActivityCommand>().Last().InitiativeId);
        Assert.True(storedFirst);
    }

    [Fact]
    public async Task A_failure_while_noting_activity_never_fails_the_message()
    {
        var id = await context.Initiatives.CreateClarifying("App");
        var view = await context.Start(id);
        context.Sender.BeforeHandle = request =>
        {
            if (request is RecordInitiativeActivityCommand)
            {
                throw new InvalidOperationException("boom");
            }
        };

        var after = await context.Send(id, view.Version, "mi respuesta");

        Assert.True(after.Version > view.Version);
    }
}
