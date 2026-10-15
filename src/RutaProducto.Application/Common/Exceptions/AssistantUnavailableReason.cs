namespace RutaProducto.Application.Common.Exceptions;

/// <summary>Why the assistant (or the drafter) could not produce a result. Callers word their own message per reason.</summary>
public enum AssistantUnavailableReason
{
    /// <summary>The call timed out, the connection failed or the service kept failing.</summary>
    Timeout,

    /// <summary>The provider quota is exhausted.</summary>
    Quota,

    /// <summary>The key was rejected.</summary>
    Auth,

    /// <summary>The model or the request was rejected (wrong model name, bad request).</summary>
    Model,

    /// <summary>The prompt or the response was blocked by the provider.</summary>
    Blocked,

    /// <summary>The response was not usable (truncated, not JSON, missing members).</summary>
    Malformed,
}
