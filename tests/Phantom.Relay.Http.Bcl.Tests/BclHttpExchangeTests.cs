// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net;
using System.Text;
using Phantom.Relay.Xbox.Http;

namespace Phantom.Relay.Http.Bcl.Tests;

public sealed class BclHttpExchangeTests
{
    private static readonly Uri Address = new("https://service.example.test/v5/play");

    [Fact]
    public async Task A_json_post_carries_its_headers_body_and_media_type()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "{\"ok\":true}"u8.ToArray());
        using var client = new HttpClient(handler);
        var exchange = new BclHttpExchange(client);
        var request = new HttpRequest(HttpVerb.Post, Address, [new HttpHeader("x-gssv-client", "XboxComBrowser")], "{\"a\":1}"u8.ToArray(), HttpRequests.JsonType);

        var reply = await exchange.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("XboxComBrowser", Assert.Single(handler.Request.Headers.GetValues("x-gssv-client")));
        Assert.Equal("application/json", handler.Request.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"a\":1}", Encoding.UTF8.GetString(handler.RequestBody!));

        Assert.Equal(200, reply.Status);
        Assert.Equal("{\"ok\":true}", Encoding.UTF8.GetString(reply.Body.Span));
    }

    [Fact]
    public async Task A_refusal_comes_back_as_its_status_not_an_exception()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, "{\"XErr\":1}"u8.ToArray());
        using var client = new HttpClient(handler);
        var exchange = new BclHttpExchange(client);

        var reply = await exchange.SendAsync(HttpRequests.Empty(HttpVerb.Delete, Address, []), CancellationToken.None);

        Assert.Equal(HttpMethod.Delete, handler.Request!.Method);
        Assert.Null(handler.Request.Content);
        Assert.True(reply.IsUnauthorized);
    }
}
