using System.Text;
using BmadPlatform.Application.Features.Context.GetAttachmentDownload;
using BmadPlatform.Web.Context;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BmadPlatform.Web.Tests.Context;

public sealed class ContextEndpointsTests
{
    [Fact]
    public async Task A_found_file_is_an_opaque_attachment_that_browsers_must_not_sniff_or_keep()
    {
        var http = NewContext();
        var download = new AttachmentDownload("informe \"x\" ñ.html", new MemoryStream(Encoding.UTF8.GetBytes("<b>hola</b>")));

        await Execute(http, download);

        Assert.Equal(StatusCodes.Status200OK, http.Response.StatusCode);
        Assert.Equal("application/octet-stream", http.Response.ContentType);
        Assert.Equal("nosniff", http.Response.Headers.XContentTypeOptions);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);

        var disposition = http.Response.Headers.ContentDisposition.ToString();
        Assert.StartsWith("attachment", disposition);
        Assert.Contains("filename*=UTF-8''", disposition);
        Assert.Equal("<b>hola</b>", Body(http));
    }

    [Fact]
    public async Task The_uniform_not_found_carries_the_same_protective_headers_and_no_body()
    {
        var http = NewContext();

        await Execute(http, null);

        Assert.Equal(StatusCodes.Status404NotFound, http.Response.StatusCode);
        Assert.Equal("nosniff", http.Response.Headers.XContentTypeOptions);
        Assert.Equal("no-store", http.Response.Headers.CacheControl);
        Assert.Equal(string.Empty, Body(http));
    }

    [Fact]
    public void The_route_requires_an_authenticated_user()
    {
        // The registrations let the route builder tell service parameters from body parameters.
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<AuthenticationStateProvider, StubAuthenticationStateProvider>();
        builder.Services.AddSingleton<IMediator>(new StubMediator(null));
        var app = builder.Build();
        app.MapContextEndpoints();

        var endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints));

        Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }

    private static DefaultHttpContext NewContext()
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        return new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };
    }

    private static async Task Execute(DefaultHttpContext http, AttachmentDownload? download)
    {
        var result = await ContextEndpoints.DownloadAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            http,
            new StubAuthenticationStateProvider(),
            new StubMediator(download),
            CancellationToken.None);

        await result.ExecuteAsync(http);
    }

    private static string Body(DefaultHttpContext http)
    {
        http.Response.Body.Position = 0;

        return new StreamReader(http.Response.Body).ReadToEnd();
    }

    private sealed class StubAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new System.Security.Claims.ClaimsPrincipal()));
    }

    private sealed class StubMediator(AttachmentDownload? download) : IMediator
    {
        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            Task.FromResult((TResponse)(object?)download!);

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default)
            where TRequest : IRequest => throw new NotSupportedException();

        public Task<object?> Send(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
