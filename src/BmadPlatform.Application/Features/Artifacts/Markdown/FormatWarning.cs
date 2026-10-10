namespace BmadPlatform.Application.Features.Artifacts.Markdown;

/// <summary>A non-blocking finding about the Markdown. <paramref name="Line"/> is null for whole-document findings.</summary>
public sealed record FormatWarning(int? Line, string Message);
