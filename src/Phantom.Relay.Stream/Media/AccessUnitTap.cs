// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Where a listener taps the stream's access units on the decode thread (<see cref="Attach"/>), and where anyone asks
/// the console for a key frame (<see cref="RequestKeyframe"/>): the network thread sends the request as a PLI on its next
/// turn, through the same path and at most as often as a loss's. Any thread attaches, detaches and asks; nothing is
/// allocated.
/// </summary>
public sealed class AccessUnitTap
{
    private IAccessUnitListener? _listener;
    private int _keyframeWanted;

    /// <summary>Whether a key frame was asked for and the request has not gone out yet.</summary>
    public bool KeyframeRequested => Volatile.Read(ref _keyframeWanted) != 0;

    /// <summary>From the next access unit on, <paramref name="listener"/> sees each one (it replaces any before).</summary>
    public void Attach(IAccessUnitListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        Volatile.Write(ref _listener, listener);
    }

    /// <summary>No listener sees the access units from the next one on.</summary>
    public void Detach()
    {
        Volatile.Write(ref _listener, null);
    }

    /// <summary>Asks the console for a key frame, from any thread; the network thread sends it on its next turn.</summary>
    public void RequestKeyframe()
    {
        Volatile.Write(ref _keyframeWanted, 1);
    }

    /// <summary>The network thread sent the request.</summary>
    public void KeyframeAsked()
    {
        Volatile.Write(ref _keyframeWanted, 0);
    }

    /// <summary>The decode thread took <paramref name="unit"/>: the listener, if any, sees it.</summary>
    [HotPath]
    public void Offer(byte[] unit, uint rtpTimestamp, bool key)
    {
        var listener = Volatile.Read(ref _listener);

        if (listener is not null)
        {
            listener.Received(unit, rtpTimestamp, key);
        }
    }
}
