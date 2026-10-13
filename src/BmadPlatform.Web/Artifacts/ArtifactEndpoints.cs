using System.Text;
using BmadPlatform.Application.Features.Artifacts.ExportArtifact;
using BmadPlatform.Domain.Artifacts;
using BmadPlatform.Web.Components.Artifacts;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;

namespace BmadPlatform.Web.Artifacts;

internal static class ArtifactEndpoints
{
    public static string DownloadPath(Guid initiativeId, ArtifactType type) =>
        $"/iniciativas/{initiativeId}/artefactos/{ArtifactLabels.Slug(type)}/descargar";

    /// <summary>
    /// The .md download is a plain GET endpoint because a file needs an HTTP response, which interactive components
    /// do not have. It goes through the same query pipeline (ownership included) as the page.
    /// </summary>
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGet("/iniciativas/{id:guid}/artefactos/{slug}/descargar", DownloadAsync)
            .RequireAuthorization();

        return endpoints;
    }

    internal static async Task<IResult> DownloadAsync(
        Guid id,
        string slug,
        HttpContext httpContext,
        AuthenticationStateProvider authenticationStateProvider,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        if (!ArtifactLabels.TryParseSlug(slug, out var type))
        {
            return Results.NotFound();
        }

        // Outside a Blazor render the authentication state is empty, and the current-user service reads it:
        // hand it the user of this request.
        if (authenticationStateProvider is IHostEnvironmentAuthenticationStateProvider host)
        {
            host.SetAuthenticationState(Task.FromResult(new AuthenticationState(httpContext.User)));
        }

        var export = await mediator.Send(new ExportArtifactQuery(id, type), cancellationToken);

        if (export is null)
        {
            return Results.NotFound();
        }

        // The document can describe a private initiative: browsers and proxies should not keep a copy.
        httpContext.Response.Headers.CacheControl = "no-store";

        return Results.File(Encoding.UTF8.GetBytes(export.Content), export.ContentType, export.FileName);
    }
}
