// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Http;

/// <summary>
/// One HTTPS request and its answer. Heads implement it (the BCL client on the desktop, fetch through a same-origin
/// relay in the browser) until the engine's HTTPS client port lands and replaces it.
/// </summary>
public interface IHttpExchange
{
    /// <summary>Sends <paramref name="request"/> and returns the answer, whatever its status.</summary>
    Task<HttpReply> SendAsync(HttpRequest request, CancellationToken cancellation);
}
