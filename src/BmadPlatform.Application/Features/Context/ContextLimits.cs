namespace BmadPlatform.Application.Features.Context;

/// <summary>
/// The limits of an initiative's context. Only the per-file cap comes from configuration (it is registered by
/// Infrastructure); the item count and the total size are fixed product rules.
/// </summary>
public sealed record ContextLimits(long MaxFileBytes)
{
    public const int MaxItems = 20;

    public const long MaxTotalBytes = 50L * 1024 * 1024;
}
