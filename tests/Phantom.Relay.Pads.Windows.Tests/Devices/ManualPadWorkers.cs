// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Pads.Windows.Devices;

namespace Phantom.Relay.Pads.Windows.Tests.Devices;

/// <summary>Readers stepped by the test on its own thread, in place of a thread each.</summary>
internal sealed class ManualPadWorkers : IPadWorkers
{
    public List<PadReader> Readers { get; } = [];

    public void Start(PadReader reader)
    {
        reader.Begin();
        Readers.Add(reader);
    }

    /// <summary>One step of every reader still running; a reader whose pad is gone finishes, as its thread would.</summary>
    public void StepAll()
    {
        foreach (var reader in Readers)
        {
            if (!reader.Finished && !reader.Step())
            {
                reader.Finish();
            }
        }
    }

    public void Join()
    {
        foreach (var reader in Readers)
        {
            reader.Finish();
        }
    }
}
