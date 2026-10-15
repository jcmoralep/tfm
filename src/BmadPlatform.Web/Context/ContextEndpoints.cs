using BmadPlatform.Application.Features.Context.GetAttachmentDownload;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;

namespace BmadPlatform.Web.Context;

internal static class ContextEndpoints
{
    public static string DownloadPath(Guid initiativeId, Guid attachmentId) =>
        $"/iniciativas/{initiativeId}/contexto/{attachmentId}/descargar";

    /// <summary>
    /// The download is a plain GET endpoint because a file needs an HTTP response, which interactive components do
    /// not have. It goes through the same query pipeline (ownership included) as the page and takes ids only.
    /// </summary>
    public static IEndpointRouteBuilder MapContextEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/iniciativas/{id:guid}/contexto/{attachmentId:guid}/descargar", DownloadAsync)
            .RequireAuthorization();

        return endpoints;
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        Guid attachmentId,
        HttpContext httpContext,
        AuthenticationStateProvider authenticationStateProvider,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Outside a Blazor render the authentication state is empty, and the current-user service reads it:
        // hand it the user of this request.
        if (authenticationStateProvider is IHostEnvironmentAuthenticationStateProvider host)
        {
            host.SetAuthenticationState(Task.FromResult(new AuthenticationState(httpContext.User)));
        }

        var download = await mediator.Send(new GetAttachmentDownloadQuery(id, attachmentId), cancellationToken);

        if (download is null)
        {
            return Results.NotFound();
        }

        // A user-supplied file must never be interpreted by the browser: always an opaque attachment, no sniffing,
        // and no copy kept by browsers or proxies. Results.File disposes the stream and writes Content-Disposition.
        httpContext.Response.Headers.XContentTypeOptions = "nosniff";
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.File(download.Content, "application/octet-stream", download.FileName, enableRangeProcessing: false);
    }
}
