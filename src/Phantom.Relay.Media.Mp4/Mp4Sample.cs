// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>One sample of a run: its size in bytes, its duration in the track's units and its trun sample flags.</summary>
internal readonly record struct Mp4Sample(int Size, uint Duration, uint Flags);
