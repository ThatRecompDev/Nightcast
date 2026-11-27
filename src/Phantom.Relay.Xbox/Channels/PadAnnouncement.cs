// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Channels;

/// <summary>
/// When the first pad is announced on the control channel, and when its reports may go out (docs/architecture/
/// xhome.md#the-first-pad). The console binds the pad to the session at its first report, and a report that comes
/// while the game is still constrained leaves the pad dead for the whole session; so nothing is announced until the
/// console has completed the un-constrain transaction (or <see cref="ReadyFallback"/> after the description at worst).
/// Announced with the other greetings, on another channel (SCTP keeps no order between streams), the pad raced the
/// console's setup of the input channel and about half the time stayed dead. The console answers a pad it took with
/// server metadata on the input channel; without that answer within <see cref="AnswerWait"/>, the pad is removed and
/// announced again, at most <see cref="MaxAnnouncements"/> times.
/// </summary>
public sealed class PadAnnouncement
{
    /// <summary>How long after the description the pad waits for the un-constrain's completion before going anyway.</summary>
    public static readonly TimeSpan ReadyFallback = TimeSpan.FromSeconds(3);

    /// <summary>How long the console has to answer an announcement with server metadata before it is made again.</summary>
    public static readonly TimeSpan AnswerWait = TimeSpan.FromMilliseconds(1500);

    public const int MaxAnnouncements = 4;

    private readonly TimeProvider _clock;
    private long _describedAt;
    private long _announcedAt;
    private bool _described;
    private bool _ready;
    private bool _answered;

    public PadAnnouncement(TimeProvider clock)
    {
        _clock = clock;
    }

    /// <summary>The times the pad was announced so far.</summary>
    public int Announcements { get; private set; }

    /// <summary>Whether pad reports may go out: the pad was announced once the game could take input.</summary>
    public bool ReportsMayFlow => Announcements > 0;

    /// <summary>The client's description (and the un-constrain after it) went out; the fallback starts from here.</summary>
    public void Described()
    {
        if (_described)
        {
            return;
        }

        _described = true;
        _describedAt = _clock.GetTimestamp();
    }

    /// <summary>The console completed the un-constrain transaction: the game takes input.</summary>
    public void Unconstrained()
    {
        _ready = true;
    }

    /// <summary>The console sent server metadata on the input channel: it took the pad.</summary>
    public void Answered()
    {
        _answered = true;
    }

    /// <summary>
    /// What to send now on the control channel, called while it is open: nothing, the first announcement, or a
    /// removal and a new announcement.
    /// </summary>
    public PadStep Next()
    {
        if (!_ready && _described && _clock.GetElapsedTime(_describedAt) >= ReadyFallback)
        {
            _ready = true;
        }

        if (!_ready || _answered || Announcements >= MaxAnnouncements)
        {
            return PadStep.None;
        }

        if (Announcements > 0 && _clock.GetElapsedTime(_announcedAt) < AnswerWait)
        {
            return PadStep.None;
        }

        var step = Announcements == 0 ? PadStep.Announce : PadStep.Reannounce;
        Announcements++;
        _announcedAt = _clock.GetTimestamp();

        return step;
    }
}
