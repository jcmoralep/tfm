namespace RutaProducto.Infrastructure.Assistant;

/// <summary>
/// What the model returns for a Question turn, as received. The verdict and the depth stay strings so the guard can
/// reject a value outside the contract instead of the parser failing.
/// </summary>
internal sealed record TurnReply(
    string Verdict,
    string Acknowledgement,
    string Question,
    string Example,
    string Why,
    string? SuggestedDepth = null);
