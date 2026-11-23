// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Media.Gpu;

/// <summary>
/// The decode thread's side of a decoder whose pictures stay on the GPU (<see cref="IGpuPictureSource"/> is the
/// renderer's): the <see cref="DecodeWorker"/> lets it apply what the renderer asked before each decode
/// (<see cref="Prepare"/>), and after it hands the picture on with <see cref="Publish"/> while
/// <see cref="IsSharing"/>, instead of reading it back.
/// </summary>
public interface IGpuPictureProducer
{
    /// <summary>Whether pictures go to the renderer on the GPU now.</summary>
    bool IsSharing { get; }

    /// <summary>Before a decode: applies the renderer's offer (a move to its adapter happens before the first picture only).</summary>
    void Prepare();

    /// <summary>
    /// After a decode: puts its picture in a slot of the ring and publishes it, ready at <paramref name="decodedAt"/>
    /// (a timestamp of the stream's clock), and says its visible size; false when the decode gave no picture, or the
    /// picture was not published (a repeat while frames are generated: the generated frames already fill its time).
    /// <paramref name="kind"/> says whether the unit carried a new picture (<see cref="RepeatClassifier"/>);
    /// <paramref name="rtpTimestamp"/> is the unit's RTP timestamp on the video's 90 kHz clock, the time the console
    /// gave the picture, so the real interval between two new pictures is known whatever the network did.
    /// </summary>
    bool Publish(PictureKind kind, long decodedAt, uint rtpTimestamp, out int width, out int height);

    /// <summary>
    /// After <see cref="Publish"/>, for a screenshot (<see cref="PictureGrab"/>): reads the decoded picture back to the
    /// CPU into <paramref name="target"/>, at its own size, before any upscaler or generated frame; it waits for the
    /// GPU, so it runs only when asked. False when there is no picture to read.
    /// </summary>
    bool TryReadBack(VideoFrame target);
}
