// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Relay;

/// <summary>Cloudflare's answer: the TURN servers as RTCIceServer entries, with credentials that expire on their own.</summary>
internal sealed record IceServersReply(IReadOnlyList<IceServerEntry>? IceServers);
