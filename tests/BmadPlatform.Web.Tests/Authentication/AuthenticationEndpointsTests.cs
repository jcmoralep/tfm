using BmadPlatform.Web.Authentication;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace BmadPlatform.Web.Tests.Authentication;

public sealed class AuthenticationEndpointsTests
{
    [Fact]
    public async Task Logout_with_an_invalid_antiforgery_token_redirects_home_without_signing_out()
    {
        var mediator = new ThrowingMediator();

        var result = await AuthenticationEndpoints.SignOutAsync(
            new DefaultHttpContext(),
            new StubAntiforgery(isValid: false),
            mediator,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectHttpResult>(result);
        Assert.True(redirect.AcceptLocalUrlOnly);
        Assert.Equal("~/", redirect.Url);
    }

    private sealed class StubAntiforgery(bool isValid) : IAntiforgery
    {
        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(isValid);

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext) => throw new NotSupportedException();

        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();
    }

    // Any call fails the test: an invalid token must never reach the sign-out command.
    private sealed class ThrowingMediator : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Sign-out must not run.");

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new InvalidOperationException("Sign-out must not run.");

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Sign-out must not run.");

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Sign-out must not run.");

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Sign-out must not run.");

        public Task Publish(object notification, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Sign-out must not run.");

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
            => throw new InvalidOperationException("Sign-out must not run.");
    }
}
