// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>An ICE candidate in its bare form (<c>candidate:...</c>), with its media id and line.</summary>
public sealed record IceCandidate(string Candidate, string? SdpMid, int SdpMLineIndex);
