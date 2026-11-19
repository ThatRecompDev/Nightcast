// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Http;

/// <summary>One request header.</summary>
public readonly record struct HttpHeader(string Name, string Value);
