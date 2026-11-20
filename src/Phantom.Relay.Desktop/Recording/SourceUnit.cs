// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>An access unit queued for source.mp4: the decode worker's own copy, its RTP timestamp and its key-frame mark.</summary>
internal readonly record struct SourceUnit(byte[] Unit, uint RtpTimestamp, bool Key);
