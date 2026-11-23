// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Stills;

/// <summary>
/// The one line the window shows of the screenshots: the newest notice, for its while, then nothing. Any thread posts;
/// the render thread reads <see cref="Current"/> each frame, which allocates nothing and is the same string until a new
/// notice comes or the last one's time is up.
/// </summary>
internal sealed class NoticeBoard
{
    /// <summary>How long a notice stays on screen unless it says otherwise.</summary>
    public static readonly TimeSpan ShownFor = TimeSpan.FromSeconds(6);

    private readonly TimeProvider _clock;
    private Notice? _notice;

    public NoticeBoard(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>The notice on screen now, or null.</summary>
    public string? Current
    {
        get
        {
            var notice = Volatile.Read(ref _notice);

            if (notice is null || _clock.GetElapsedTime(notice.PostedAt) >= notice.ShownFor)
            {
                return null;
            }

            return notice.Text;
        }
    }

    /// <summary>Shows <paramref name="text"/> from now for <see cref="ShownFor"/>, in place of any notice before.</summary>
    public void Post(string text)
    {
        Post(text, ShownFor);
    }

    /// <summary>Shows <paramref name="text"/> from now for <paramref name="shownFor"/>.</summary>
    public void Post(string text, TimeSpan shownFor)
    {
        ArgumentNullException.ThrowIfNull(text);
        Volatile.Write(ref _notice, new Notice(text, _clock.GetTimestamp(), shownFor));
    }
}
