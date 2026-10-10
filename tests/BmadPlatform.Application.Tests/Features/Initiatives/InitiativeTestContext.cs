using BmadPlatform.Application.Features.Initiatives;
using BmadPlatform.Application.Features.Initiatives.CompleteInitiative;
using BmadPlatform.Application.Features.Initiatives.DeleteInitiative;
using BmadPlatform.Application.Features.Initiatives.GetInitiative;
using BmadPlatform.Application.Features.Initiatives.ListInitiatives;
using BmadPlatform.Application.Features.Initiatives.SaveInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.SaveInitiativeDetails;
using BmadPlatform.Application.Features.Initiatives.SetInitiativeDepth;
using BmadPlatform.Application.Features.Initiatives.StartPlanning;
using BmadPlatform.Application.Features.Initiatives.UpdateInitiative;
using BmadPlatform.Application.Tests.TestDoubles;
using BmadPlatform.Domain.Initiatives;

namespace BmadPlatform.Application.Tests.Features.Initiatives;

/// <summary>Wires the real handlers to the in-memory doubles. Tests switch user with <see cref="User"/>.</summary>
public sealed class InitiativeTestContext
{
    public const string UserA = "user-a";
    public const string UserB = "user-b";

    public InMemoryInitiativeRepository Repository { get; } = new();

    public FakeCurrentUser User { get; } = new(UserA);

    public FixedTimeProvider Clock { get; } = new();

    public Task<Guid> SaveDetails(Guid? id, string name, string? description = null, bool advance = false) =>
        new SaveInitiativeDetailsCommandHandler(Repository, User, Clock)
            .Handle(new SaveInitiativeDetailsCommand(id, name, description, advance), default);

    public Task SaveDepth(Guid id, DepthMode? mode, InitiativeDepth? depth, bool advance = false) =>
        new SaveInitiativeDepthCommandHandler(Repository, User, Clock)
            .Handle(new SaveInitiativeDepthCommand(id, mode, depth, advance), default);

    public Task Complete(Guid id) =>
        new CompleteInitiativeCommandHandler(Repository, User, Clock).Handle(new CompleteInitiativeCommand(id), default);

    public Task StartPlanning(Guid id) =>
        new StartPlanningCommandHandler(Repository, User, Clock).Handle(new StartPlanningCommand(id), default);

    public Task SetInitiativeDepth(Guid id, InitiativeDepth depth) =>
        new SetInitiativeDepthCommandHandler(Repository, User, Clock)
            .Handle(new SetInitiativeDepthCommand(id, depth), default);

    public Task Update(Guid id, string name, string? description, DepthMode? mode, InitiativeDepth? depth) =>
        new UpdateInitiativeCommandHandler(Repository, User, Clock)
            .Handle(new UpdateInitiativeCommand(id, name, description, mode, depth), default);

    public Task Delete(Guid id) =>
        new DeleteInitiativeCommandHandler(Repository, User, Clock).Handle(new DeleteInitiativeCommand(id), default);

    public Task<InitiativeDetails?> Get(Guid id) =>
        new GetInitiativeQueryHandler(Repository, User).Handle(new GetInitiativeQuery(id), default);

    public Task<IReadOnlyList<InitiativeSummary>> List(
        string? search = null,
        InitiativeStatus? status = null,
        InitiativeDepth? depth = null) =>
        new ListInitiativesQueryHandler(Repository, User).Handle(new ListInitiativesQuery(search, status, depth), default);

    /// <summary>Forces a status the handlers cannot reach yet (Planning, ReadyToBuild) on the stored record.</summary>
    public void ForceStatus(Guid id, InitiativeStatus status) =>
        typeof(Initiative).GetProperty(nameof(Initiative.Status))!
            .SetValue(Repository.Stored.Single(initiative => initiative.Id == id), status);

    /// <summary>Creates a draft and moves the clock forward so ordering by update time is deterministic.</summary>
    public async Task<Guid> CreateDraft(string name)
    {
        var id = await SaveDetails(null, name);
        Clock.Advance(TimeSpan.FromMinutes(1));

        return id;
    }

    /// <summary>Creates an initiative that has finished the wizard (Clarifying, Manual, given depth).</summary>
    public async Task<Guid> CreateClarifying(string name, InitiativeDepth depth = InitiativeDepth.Standard)
    {
        var id = await CreateDraft(name);
        await SaveDepth(id, DepthMode.Manual, depth, advance: true);
        await Complete(id);
        Clock.Advance(TimeSpan.FromMinutes(1));

        return id;
    }
}
