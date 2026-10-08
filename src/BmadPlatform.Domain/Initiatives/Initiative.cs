using BmadPlatform.Domain.Common;

namespace BmadPlatform.Domain.Initiatives;

/// <summary>
/// A product idea owned by the user who created it. Every mutator receives the current time so the
/// aggregate never reads the clock, and every mutator refreshes <see cref="UpdatedAt"/>.
/// </summary>
public sealed class Initiative
{
    public const int NameMaxLength = 120;
    public const int DescriptionMaxLength = 1000;

    // Required by EF Core to materialize the entity.
    private Initiative()
    {
        CreatedByUserId = string.Empty;
        Name = string.Empty;
    }

    public Guid Id { get; private set; }

    public string CreatedByUserId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public InitiativeStatus Status { get; private set; }

    public DepthMode? DepthMode { get; private set; }

    public InitiativeDepth? Depth { get; private set; }

    public CreationStep CreationStep { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public static Initiative CreateDraft(string ownerId, string name, string? description, DateTimeOffset now)
    {
        var initiative = new Initiative
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = ownerId,
            Status = InitiativeStatus.Draft,
            CreationStep = CreationStep.Details,
            CreatedAt = now,
        };

        initiative.Rename(name, description, now);

        return initiative;
    }

    /// <summary>Changes name and description. Allowed in any status.</summary>
    public void Rename(string name, string? description, DateTimeOffset now)
    {
        var trimmedName = name?.Trim() ?? string.Empty;

        if (trimmedName.Length == 0)
        {
            throw new DomainException("El nombre es obligatorio.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException($"El nombre no puede superar los {NameMaxLength} caracteres.");
        }

        if (description is { Length: > DescriptionMaxLength })
        {
            throw new DomainException($"La descripción no puede superar los {DescriptionMaxLength} caracteres.");
        }

        Name = trimmedName;
        Description = string.IsNullOrWhiteSpace(description) ? null : description;
        UpdatedAt = now;
    }

    /// <summary>
    /// Sets the depth mode and level. Only possible in Draft or Clarifying. Automatic mode always clears the level.
    /// A draft may keep an unfinished choice; once it is Clarifying, Manual needs a level.
    /// </summary>
    public void SetDepth(DepthMode? mode, InitiativeDepth? depth, DateTimeOffset now)
    {
        if (Status is not (InitiativeStatus.Draft or InitiativeStatus.Clarifying))
        {
            throw new DomainException("El modo y la profundidad solo se pueden cambiar en Borrador o Aclarando.");
        }

        if (mode is null && depth is not null)
        {
            throw new DomainException("Elige un modo de profundidad antes de elegir un nivel.");
        }

        if (Status != InitiativeStatus.Draft)
        {
            if (mode is null)
            {
                throw new DomainException("Elige un modo de profundidad.");
            }

            if (mode == Initiatives.DepthMode.Manual && depth is null)
            {
                throw new DomainException("Elige un nivel de profundidad.");
            }
        }

        DepthMode = mode;
        Depth = mode == Initiatives.DepthMode.Automatic ? null : depth;
        UpdatedAt = now;
    }

    /// <summary>Records the wizard step to reopen. Only drafts have a wizard.</summary>
    public void MoveToStep(CreationStep step, DateTimeOffset now)
    {
        if (Status != InitiativeStatus.Draft)
        {
            throw new DomainException("Solo se puede modificar el asistente de una iniciativa en Borrador.");
        }

        CreationStep = step;
        UpdatedAt = now;
    }

    /// <summary>Finishes the wizard: Draft becomes Clarifying.</summary>
    public void Complete(DateTimeOffset now)
    {
        if (Status != InitiativeStatus.Draft)
        {
            throw new DomainException("Solo se puede finalizar una iniciativa en Borrador.");
        }

        if (DepthMode is null)
        {
            throw new DomainException("Elige un modo de profundidad antes de finalizar.");
        }

        if (DepthMode == Initiatives.DepthMode.Manual && Depth is null)
        {
            throw new DomainException("Elige un nivel de profundidad antes de finalizar.");
        }

        Status = InitiativeStatus.Clarifying;
        CreationStep = CreationStep.Review;
        UpdatedAt = now;
    }

    /// <summary>Soft delete. Deleting an already deleted initiative changes nothing.</summary>
    public void Delete(DateTimeOffset now)
    {
        if (DeletedAt is not null)
        {
            return;
        }

        DeletedAt = now;
        UpdatedAt = now;
    }
}
