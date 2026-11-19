// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>A region of the streaming service: its name and the base of its REST calls.</summary>
public sealed record StreamingRegion(string Name, Uri BaseUri, bool IsDefault);
