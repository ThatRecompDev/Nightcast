// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Recording;

/// <summary>A shown picture queued for shown.mp4: the encoder's pool texture it was copied into and when it went on screen (the clock's).</summary>
internal readonly record struct ShownFrame(int Slot, long ShownAt);
