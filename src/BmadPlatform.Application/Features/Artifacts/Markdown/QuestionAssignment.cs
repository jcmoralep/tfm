namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>The owner and the revisit condition to write on one open question; an empty value writes the default.</summary>
public sealed record QuestionAssignment(string Id, string? Owner, string? Revisit);
