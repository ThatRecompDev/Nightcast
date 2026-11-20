// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// One recording as recording.txt tells it: when it started (local time), the window's settings then, the multiplier
/// and rate the shown pictures came at, the labels, and how each file came out.
/// </summary>
internal sealed record RecordingSummary(
    DateTimeOffset StartedAt,
    RecordingSettings Settings,
    int Multiplier,
    int ShownRate,
    string SourceLabel,
    string ShownLabel,
    SourceOutcome Source,
    ShownOutcome Shown);
