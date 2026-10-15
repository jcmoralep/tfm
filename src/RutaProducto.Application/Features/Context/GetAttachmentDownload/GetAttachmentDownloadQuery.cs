using MediatR;

namespace RutaProducto.Application.Features.Context.GetAttachmentDownload;

/// <summary>
/// Returns <c>null</c> for everything that is not a downloadable file of the caller: a foreign, deleted or unknown
/// initiative, a foreign or unknown attachment, a link, a note, or a file whose bytes are missing.
/// </summary>
public sealed record GetAttachmentDownloadQuery(Guid InitiativeId, Guid AttachmentId) : IRequest<AttachmentDownload?>;

/// <summary>The normalized file name and the stored bytes. The caller owns and must dispose the stream.</summary>
public sealed record AttachmentDownload(string FileName, Stream Content);
