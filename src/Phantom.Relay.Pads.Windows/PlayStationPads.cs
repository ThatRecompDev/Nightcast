// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Pads.Windows.Devices;
using Phantom.Relay.Pads.Windows.Hid;
using Phantom.Relay.Pads.Windows.Reports;

namespace Phantom.Relay.Pads.Windows;

/// <summary>
/// The PlayStation pads on Windows (DualSense, DualSense Edge, DualShock 4; over USB or Bluetooth) through the system's
/// HID API, for the engine: their state by slot (<see cref="IGamepadSource"/>), their rumble and light bar
/// (<see cref="IGamepadFeedback"/>) and their family (<see cref="IGamepadFamilies"/>, always PlayStation). A
/// discovery thread looks for pads every <see cref="RescanInterval"/> and gives each pad found a free slot and a reader
/// thread of its own; a pad that goes away frees its slot, and comes back in the first free one. The frame only copies
/// the newest state each reader left (<see cref="Poll"/>, allocation-free) and leaves feedback for the reader to write,
/// so a pad never holds the frame up. <see cref="Start"/> begins it all, <see cref="Stop"/> ends it.
/// </summary>
public sealed class PlayStationPads : IGamepadSource, IGamepadFeedback, IGamepadFamilies, IDisposable
{
    /// <summary>NOT DATA: how often the discovery thread looks for pads plugged in or paired since.</summary>
    public static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(2);

    private const string DiscoveryThreadName = "PlayStation pad discovery";

    private readonly IHidHost _host;
    private readonly IPadWorkers _workers;
    private readonly PadSlot[] _slots;
    private readonly PadReader?[] _readers;
    private readonly string?[] _paths;
    private readonly List<string> _present = [];
    private readonly HashSet<string> _ignored = new(StringComparer.OrdinalIgnoreCase);
    private readonly ManualResetEventSlim _stopping = new();
    private Thread? _discovery;

    /// <summary>The pads Windows' HID API finds, each read on a thread of its own; nothing runs before <see cref="Start"/>.</summary>
    [SupportedOSPlatform("windows")]
    public PlayStationPads()
        : this(new WindowsHidHost(), new ThreadPadWorkers())
    {
    }

    internal PlayStationPads(IHidHost host, IPadWorkers workers)
    {
        _host = host;
        _workers = workers;
        _slots = new PadSlot[InputLimits.DefaultGamepads];
        _readers = new PadReader?[_slots.Length];
        _paths = new string?[_slots.Length];

        for (var slot = 0; slot < _slots.Length; slot++)
        {
            _slots[slot] = new PadSlot();
        }
    }

    /// <summary>Starts looking for pads on a thread of its own; pads found then get a reader thread each.</summary>
    public void Start()
    {
        if (_discovery is not null)
        {
            return;
        }

        _stopping.Reset();
        _discovery = new Thread(Discover) { IsBackground = true, Name = DiscoveryThreadName };
        _discovery.Start();
    }

    /// <summary>Stops looking, ends every reader and closes every pad; the slots are empty after.</summary>
    public void Stop()
    {
        _stopping.Set();
        _discovery?.Join();
        _discovery = null;

        foreach (var reader in _readers)
        {
            reader?.Stop();
        }

        _workers.Join();
        Array.Clear(_readers);
        Array.Clear(_paths);
    }

    public void Dispose()
    {
        Stop();
        _stopping.Dispose();
    }

    [HotPath]
    public void Poll(Span<GamepadState> pads)
    {
        pads.Clear();
        var count = Math.Min(pads.Length, _slots.Length);

        for (var slot = 0; slot < count; slot++)
        {
            pads[slot] = _slots[slot].State();
        }
    }

    [HotPath]
    public GamepadFamily Family(int slot)
    {
        if (!IsSlot(slot))
        {
            return GamepadFamily.Unknown;
        }

        return _slots[slot].Family();
    }

    public bool TrySetRumble(int gamepad, float lowFrequency, float highFrequency)
    {
        if (!IsSlot(gamepad))
        {
            return false;
        }

        return _slots[gamepad].TrySetRumble(PadLevels.ToByte(lowFrequency), PadLevels.ToByte(highFrequency));
    }

    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        if (!IsSlot(gamepad))
        {
            return false;
        }

        return _slots[gamepad].TrySetLight(PadLevels.ToByte(red), PadLevels.ToByte(green), PadLevels.ToByte(blue));
    }

    /// <summary>
    /// The newest motion of the pad in <paramref name="slot"/> (raw gyro and accelerometer counts, see
    /// <see cref="PadMotion"/>); false when the slot is empty. A side channel: <see cref="GamepadState"/> has no motion.
    /// </summary>
    [HotPath]
    public bool TryGetMotion(int slot, out PadMotion motion)
    {
        motion = default;

        return IsSlot(slot) && _slots[slot].TryMotion(out motion);
    }

    /// <summary>
    /// One look for pads: frees the slots of readers that ended, stops the readers of pads no longer present, and opens
    /// every PlayStation pad not yet read into a free slot. The discovery thread runs it; a test calls it directly.
    /// </summary>
    internal void Rescan()
    {
        _present.Clear();
        _host.Enumerate(_present);
        ReleaseEnded();
        StopMissing();
        _ignored.IntersectWith(_present);

        foreach (var path in _present)
        {
            if (!_ignored.Contains(path) && SlotOf(path) < 0)
            {
                TryOpen(path);
            }
        }
    }

    private void Discover()
    {
        do
        {
            Rescan();
        }
        while (!_stopping.Wait(RescanInterval));
    }

    private void ReleaseEnded()
    {
        for (var slot = 0; slot < _readers.Length; slot++)
        {
            var reader = _readers[slot];

            if (reader is not null && reader.Finished)
            {
                _readers[slot] = null;
                _paths[slot] = null;
            }
        }
    }

    private void StopMissing()
    {
        for (var slot = 0; slot < _readers.Length; slot++)
        {
            var path = _paths[slot];

            if (path is not null && !_present.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                _readers[slot]?.Stop();
            }
        }
    }

    private void TryOpen(string path)
    {
        if (!_host.TryIdentify(path, out var vendor, out var product))
        {
            return;
        }

        if (!PlayStationProducts.TryModel(vendor, product, out var model))
        {
            _ignored.Add(path);

            return;
        }

        var slot = SlotOf(null);

        if (slot < 0)
        {
            return;
        }

        var device = _host.Open(path);

        if (device is null)
        {
            return;
        }

        var reader = new PadReader(device, model, _slots[slot]);
        _readers[slot] = reader;
        _paths[slot] = path;
        _slots[slot].Assign(reader);
        _workers.Start(reader);
    }

    /// <summary>The slot read from <paramref name="path"/> (a free slot for null); -1 when there is none.</summary>
    private int SlotOf(string? path)
    {
        for (var slot = 0; slot < _paths.Length; slot++)
        {
            if (string.Equals(_paths[slot], path, StringComparison.OrdinalIgnoreCase))
            {
                return slot;
            }
        }

        return -1;
    }

    [HotPath]
    private bool IsSlot(int slot)
    {
        return slot >= 0 && slot < _slots.Length;
    }
}
