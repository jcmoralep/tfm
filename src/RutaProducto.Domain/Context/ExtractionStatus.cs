namespace RutaProducto.Domain.Context;

/// <summary>What happened when the server tried to read the text of a file; stored as a string.</summary>
public enum ExtractionStatus
{
    /// <summary>No extractor exists for this item (links, notes, other file types) or it was never tried.</summary>
    NotApplicable,

    /// <summary>The text was read and is stored in <see cref="Attachment.ExtractedText"/>.</summary>
    Extracted,

    /// <summary>An extractor exists for the file type but could not read this file (corrupt, protected, too big, empty).</summary>
    Unreadable,
}
