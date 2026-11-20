// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Sees every access unit the <see cref="DecodeWorker"/> takes, on the decode thread, before it is decoded (a recorder
/// keeping the stream as it came). It must return at once: queue the unit and do the work on a thread of its own, never
/// block. The unit's array is the worker's own copy and is never written again, so it may be kept.
/// </summary>
public interface IAccessUnitListener
{
    /// <summary>
    /// One whole access unit in Annex-B form, its RTP timestamp on the video's 90 kHz clock, and whether it holds a key
    /// frame (an IDR).
    /// </summary>
    void Received(byte[] unit, uint rtpTimestamp, bool key);
}
