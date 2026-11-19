// Copyright (c) 2026 Phantom.Relay. All rights reserved.

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
    /// (a timestamp of the stream's clock), and says its visible size; false when the decode gave no picture.
    /// </summary>
    bool Publish(long decodedAt, out int width, out int height);
}
