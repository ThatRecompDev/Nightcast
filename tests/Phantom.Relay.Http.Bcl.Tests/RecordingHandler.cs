// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net;

namespace Phantom.Relay.Http.Bcl.Tests;

/// <summary>An in-memory handler: records the request (with its body read) and answers with a fixed response.</summary>
public sealed class RecordingHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _status;
    private readonly byte[] _body;

    public RecordingHandler(HttpStatusCode status, byte[] body)
    {
        _status = status;
        _body = body;
    }

    public HttpRequestMessage? Request { get; private set; }

    public byte[]? RequestBody { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;

        if (request.Content is not null)
        {
            RequestBody = await request.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        return new HttpResponseMessage(_status) { Content = new ByteArrayContent(_body) };
    }
}
