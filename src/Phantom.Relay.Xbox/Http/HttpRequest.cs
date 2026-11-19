// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Http;

/// <summary>A request: its verb, address, headers and body (empty for none), with the body's media type.</summary>
public sealed record HttpRequest(
    HttpVerb Verb,
    Uri Address,
    IReadOnlyList<HttpHeader> Headers,
    ReadOnlyMemory<byte> Body,
    string? ContentType);
