// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Tools.DecodeProbe;

/// <summary>
/// The average and worst of one measured step over the pictures, the first picture apart: it sets the decoder's
/// surfaces up once and is not what a running stream pays.
/// </summary>
internal sealed class TimingTally
{
    private TimeSpan _total;
    private int _count;

    public TimeSpan First { get; private set; }

    public TimeSpan Worst { get; private set; }

    public TimeSpan Average
    {
        get
        {
            if (_count == 0)
            {
                return TimeSpan.Zero;
            }

            return _total / _count;
        }
    }

    public void Add(int picture, TimeSpan spent)
    {
        if (picture == 0)
        {
            First = spent;

            return;
        }

        _total += spent;
        _count++;

        if (spent > Worst)
        {
            Worst = spent;
        }
    }

    public string Describe()
    {
        return $"{Average.TotalMilliseconds:F2} ms average, {Worst.TotalMilliseconds:F2} ms worst (the first: {First.TotalMilliseconds:F2} ms)";
    }
}
