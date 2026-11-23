// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// The stream's sound on its way to its sink, where a listener taps it (a recording, <see cref="Attach"/>): each packet
/// goes to the sink as before, then to the listener, if any, on the same network thread. Any thread attaches and
/// detaches; nothing is allocated.
/// </summary>
public sealed class AudioPacketTap : IStreamAudioSink
{
    private readonly IStreamAudioSink _sink;
    private IAudioPacketListener? _listener;

    public AudioPacketTap(IStreamAudioSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sink = sink;
    }

    /// <summary>From the next packet on, <paramref name="listener"/> sees each one (it replaces any before).</summary>
    public void Attach(IAudioPacketListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        Volatile.Write(ref _listener, listener);
    }

    /// <summary>No listener sees the packets from the next one on.</summary>
    public void Detach()
    {
        Volatile.Write(ref _listener, null);
    }

    [HotPath]
    public void Push(ReadOnlySpan<byte> payload, ushort sequence, uint timestamp)
    {
        _sink.Push(payload, sequence, timestamp);

        var listener = Volatile.Read(ref _listener);

        if (listener is not null)
        {
            listener.Received(payload, timestamp);
        }
    }
}
