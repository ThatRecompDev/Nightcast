// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Stream.Media;

/// <summary>
/// A screenshot's way to the console's own picture: any thread asks for the next decoded picture
/// (<see cref="Request"/>), and the decode thread, which checks <see cref="IsWanted"/> after each picture, copies it to
/// the CPU (read back from the GPU where it stays there) and hands it over (<see cref="Complete"/>). The picture is
/// the one decoded at the stream's own size, before any upscaler, generated frame or overlay. The caller's
/// continuation never runs on the decode thread, so the encoding waits on nothing of it. One request at a time.
/// </summary>
public sealed class PictureGrab
{
    private TaskCompletionSource<VideoFrame>? _pending;

    /// <summary>Whether a request waits for the next picture (decode thread, after each picture).</summary>
    public bool IsWanted => Volatile.Read(ref _pending) is not null;

    /// <summary>Asks for the next decoded picture; null when a request already waits for one.</summary>
    public Task<VideoFrame>? Request()
    {
        var source = new TaskCompletionSource<VideoFrame>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (Interlocked.CompareExchange(ref _pending, source, null) is not null)
        {
            return null;
        }

        return source.Task;
    }

    /// <summary>Decode thread: <paramref name="picture"/>, the caller's own copy from now on, answers the request.</summary>
    public void Complete(VideoFrame picture)
    {
        ArgumentNullException.ThrowIfNull(picture);
        Interlocked.Exchange(ref _pending, null)?.TrySetResult(picture);
    }

    /// <summary>The request ends without a picture (none came in time, or the stream ended); a new one may be made.</summary>
    public void Cancel()
    {
        Interlocked.Exchange(ref _pending, null)?.TrySetCanceled();
    }

    /// <summary>A copy of <paramref name="source"/>'s planes and colour into <paramref name="target"/>.</summary>
    public static void Copy(VideoFrame source, VideoFrame target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Prepare(source.Width, source.Height, source.ChromaWidth, source.ChromaHeight, alpha: false);
        source.Luma.CopyTo(target.Luma);
        source.Cb.CopyTo(target.Cb);
        source.Cr.CopyTo(target.Cr);

        target.Colour = source.Colour;
        target.TimestampNanoseconds = source.TimestampNanoseconds;
    }
}
