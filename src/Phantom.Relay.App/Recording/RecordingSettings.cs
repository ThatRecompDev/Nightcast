// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Recording;

/// <summary>
/// What the window shows with as a recording starts, for its labels and its notes: what the frame generation does
/// (the stats' text, "LSFG x2 (quality, flow 1.00) 0.8 ms" or "LSFG off: ..."), the multiplier chosen (1 when off)
/// and the upscaler's description.
/// </summary>
public sealed record RecordingSettings(string FrameGeneration, int Multiplier, string Upscaler);
