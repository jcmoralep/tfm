namespace RutaProducto.Application.Features.Context;

/// <summary>
/// The limits of an initiative's context. Only the per-file cap comes from configuration (it is registered by
/// Infrastructure); the item count and the total size are fixed product rules.
/// </summary>
public sealed record ContextLimits(long MaxFileBytes)
{
    /// <summary>How long reading the text of an uploaded document may take before the file is left unreadable.</summary>
    public static readonly TimeSpan DefaultExtractionTimeout = TimeSpan.FromSeconds(15);

    public TimeSpan ExtractionTimeout { get; init; } = DefaultExtractionTimeout;

    public const int MaxItems = 20;

    public const long MaxTotalBytes = 50L * 1024 * 1024;
}
