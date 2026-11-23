// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// A place playback can start, for the index at the end (tfra): a sync sample's time in its track's units, the file
/// offset of the moof it is in, its traf's number in that moof and its number in the run (both from 1).
/// </summary>
internal readonly record struct RandomAccessPoint(long Time, long FragmentOffset, int TrackFragment, int Sample);
