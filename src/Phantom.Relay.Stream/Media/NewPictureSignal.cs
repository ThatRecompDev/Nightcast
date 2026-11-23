// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// Tells the render thread that the game drew a new picture: the decode thread signals each unit the
/// <see cref="RepeatClassifier"/> calls new once it was handed off (<see cref="Arrived"/>), and the render thread waits
/// for the next one (<see cref="Wait"/>). A window that presents only new pictures waits here before it draws, so an
/// external frame generator (lsfg-vk on SteamOS) sees one present per picture the game drew, never a repeat. The
/// wait wakes the moment a picture arrives, not on a timer tick.
/// </summary>
public sealed class NewPictureSignal : IDisposable
{
    private readonly ManualResetEventSlim _arrived = new(false);
    private long _news;
    private long _seen;

    /// <summary>The decode thread: a unit of <paramref name="kind"/> was handed to the renderer.</summary>
    [HotPath]
    public void Arrived(PictureKind kind)
    {
        if (kind != PictureKind.New)
        {
            return;
        }

        Interlocked.Increment(ref _news);
        _arrived.Set();
    }

    /// <summary>
    /// The render thread: true once a new picture arrived since the last true, waiting at most <paramref name="longest"/>
    /// for one; false when none came in that time.
    /// </summary>
    [HotPath]
    public bool Wait(TimeSpan longest)
    {
        // Reset before the count is read: a picture that arrives after the read sets the event again.
        _arrived.Reset();

        if (TakeNew())
        {
            return true;
        }

        _arrived.Wait(longest);

        return TakeNew();
    }

    public void Dispose()
    {
        _arrived.Dispose();
    }

    [HotPath]
    private bool TakeNew()
    {
        var news = Interlocked.Read(ref _news);

        if (news == _seen)
        {
            return false;
        }

        _seen = news;

        return true;
    }
}
