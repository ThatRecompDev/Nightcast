// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Srtp;

/// <summary>One direction's SRTP master key and master salt, as the DTLS-SRTP exporter gives them.</summary>
public sealed record SrtpMasterKey(byte[] Key, byte[] Salt);
