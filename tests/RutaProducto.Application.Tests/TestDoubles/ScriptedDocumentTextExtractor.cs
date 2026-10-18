using RutaProducto.Application.Abstractions.Documents;

namespace RutaProducto.Application.Tests.TestDoubles;

/// <summary>Extractor for .docx and .pdf files whose outcome the test scripts: a result, an exception, or never finishing.</summary>
public sealed class ScriptedDocumentTextExtractor : IDocumentTextExtractor
{
    public DocumentTextResult Result { get; set; } = DocumentTextResult.Extracted("# Titulo\n\ntexto");

    public Exception? Throws { get; set; }

    /// <summary>When set, the call ignores its token and waits for this task, like an extractor stuck in a loop.</summary>
    public Task? Blocks { get; set; }

    public int Calls { get; private set; }

    public bool Supports(string extension) => extension.ToLowerInvariant() is ".docx" or ".pdf";

    public async Task<DocumentTextResult> ExtractAsync(Stream content, CancellationToken cancellationToken)
    {
        Calls++;

        if (Throws is not null)
        {
            throw Throws;
        }

        if (Blocks is not null)
        {
            await Blocks;
        }

        return Result;
    }
}
