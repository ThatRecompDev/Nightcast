// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Pads.Windows.Devices;

/// <summary>A background thread per reader; the threads of readers that ended are forgotten at the next start.</summary>
internal sealed class ThreadPadWorkers : IPadWorkers
{
    private const string ThreadName = "PlayStation pad reader";

    private readonly List<Thread> _threads = [];

    public void Start(PadReader reader)
    {
        _threads.RemoveAll(static thread => !thread.IsAlive);
        var thread = new Thread(reader.Run) { IsBackground = true, Name = ThreadName };
        _threads.Add(thread);
        thread.Start();
    }

    public void Join()
    {
        foreach (var thread in _threads)
        {
            thread.Join();
        }

        _threads.Clear();
    }
}
