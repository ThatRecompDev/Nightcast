// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Windows.Recording;

namespace Phantom.Relay.Desktop.Recording;

/// <summary>
/// How source.mp4 came out: what it holds (null when it was not made), why not, the units written, those skipped before
/// the first key frame, those dropped because the writer fell behind (queue full) and those the sink refused.
/// </summary>
internal sealed record SourceOutcome(Mp4Facts? Facts, string? Problem, long Written, long Skipped, long Dropped, long Failed);
