using BmadPlatform.Application.Features.Authentication.SignOut;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;

namespace BmadPlatform.Web.Authentication;

internal static class AuthenticationEndpoints
{
    public const string LoginPath = "/login";
    public const string LogoutPath = "/logout";

    /// <summary>
    /// Sign-out runs as a plain POST endpoint because removing the auth cookie needs an HTTP response,
    /// which interactive components do not have.
    /// </summary>
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(LogoutPath, SignOutAsync)
            .RequireAuthorization();

        return endpoints;
    }

    internal static async Task<IResult> SignOutAsync(
        HttpContext httpContext,
        IAntiforgery antiforgery,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        // Minimal API endpoints only validate antiforgery tokens automatically when they bind form data,
        // so the token posted by the layout form is validated explicitly.
        if (!await antiforgery.IsRequestValidAsync(httpContext))
        {
            // Typically an expired token: reload the home page so the layout renders a fresh one.
            // The user stays signed in and the request is not processed.
            return Results.LocalRedirect("~/");
        }

        await mediator.Send(new SignOutCommand(), cancellationToken);
        return Results.LocalRedirect("~" + LoginPath);
    }
}
