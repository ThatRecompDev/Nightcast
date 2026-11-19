// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net.Http.Headers;
using Phantom.Relay.Xbox.Http;

namespace Phantom.Relay.Http.Bcl;

/// <summary>
/// <see cref="IHttpExchange"/> over the BCL's <see cref="HttpClient"/>, for the desktop (and the browser, where the
/// client runs over fetch). The engine's HTTPS client port replaces it when its lane lands; the PS5 waits for that.
/// The caller owns the client and its lifetime.
/// </summary>
public sealed class BclHttpExchange : IHttpExchange
{
    private readonly HttpClient _client;

    public BclHttpExchange(HttpClient client)
    {
        _client = client;
    }

    public async Task<HttpReply> SendAsync(HttpRequest request, CancellationToken cancellation)
    {
        using var message = ToMessage(request);
        using var response = await _client.SendAsync(message, HttpCompletionOption.ResponseContentRead, cancellation);
        byte[] body = await response.Content.ReadAsByteArrayAsync(cancellation);

        return new HttpReply((int)response.StatusCode, body);
    }

    private static HttpRequestMessage ToMessage(HttpRequest request)
    {
        var message = new HttpRequestMessage(Method(request.Verb), request.Address);

        foreach (var header in request.Headers)
        {
            message.Headers.TryAddWithoutValidation(header.Name, header.Value);
        }

        if (request.ContentType is not null)
        {
            var content = new ByteArrayContent(request.Body.ToArray());
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(request.ContentType);
            message.Content = content;
        }

        return message;
    }

    private static HttpMethod Method(HttpVerb verb)
    {
        switch (verb)
        {
            case HttpVerb.Post:
                return HttpMethod.Post;
            case HttpVerb.Delete:
                return HttpMethod.Delete;
            default:
                return HttpMethod.Get;
        }
    }
}
