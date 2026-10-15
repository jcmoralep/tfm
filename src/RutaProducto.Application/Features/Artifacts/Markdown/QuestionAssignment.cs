namespace RutaProducto.Application.Features.Artifacts.Markdown;

/// <summary>The owner and the revisit condition to write on one open question; an empty value writes the default.</summary>
public sealed record QuestionAssignment(string Id, string? Owner, string? Revisit)
{
    // The synthesized ToString would print the values typed by the user.
    public override string ToString() => $"{nameof(QuestionAssignment)} {{ Id = {Id}, Owner = [redacted], Revisit = [redacted] }}";
}
