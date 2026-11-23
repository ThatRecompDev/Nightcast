// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Pads.Linux.Devices;
using Phantom.Relay.Pads.Linux.Interop;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux;

/// <summary>
/// The pads on Linux through the kernel's evdev interface, for the engine: their state by slot
/// (<see cref="IGamepadSource"/>), their rumble (<see cref="IGamepadFeedback"/>; no light: evdev has none to set) and
/// their family (<see cref="IGamepadFamilies"/>). On SteamOS that is Steam Input's virtual Xbox 360 pad (the ROG Ally's
/// or the Deck's own controls, or any pad Steam Input maps), and any other pad the kernel reads as a gamepad. A
/// discovery thread looks at <c>/dev/input/event*</c> every <see cref="RescanInterval"/> and gives each pad found a free
/// slot and a reader thread of its own; a pad that goes away frees its slot. The pads Steam asks a game not to read
/// itself (<see cref="IgnoreVariable"/>) are left alone. The frame only copies the newest state each reader left
/// (<see cref="Poll"/>, allocation-free). An engine candidate: game-agnostic, the Linux twin of the engine's XInputGamepads.
/// </summary>
public sealed class EvdevPads : IGamepadSource, IGamepadFeedback, IGamepadFamilies, IDisposable
{
    /// <summary>NOT DATA: how often the discovery thread looks for pads plugged in or paired since.</summary>
    public static readonly TimeSpan RescanInterval = TimeSpan.FromSeconds(2);

    /// <summary>The environment variable Steam lists the pads it handles itself in; the head reads it and hands it in.</summary>
    public const string IgnoreVariable = IgnoredPads.Variable;

    private const string DiscoveryThreadName = "evdev pad discovery";

    /// <summary>The engine's rumble levels (0 to 1) as the kernel's motor magnitudes (0 to 65535).</summary>
    private const float FullMagnitude = ushort.MaxValue;

    private readonly IEvdevHost _host;
    private readonly IPadWorkers _workers;
    private readonly IgnoredPads _skipped;
    private readonly EvdevSlot[] _slots;
    private readonly EvdevReader?[] _readers;
    private readonly string?[] _paths;
    private readonly List<string> _present = [];
    private readonly HashSet<string> _ignored = new(StringComparer.Ordinal);
    private readonly ManualResetEventSlim _stopping = new();
    private Thread? _discovery;

    /// <summary>
    /// The pads Linux's evdev finds, each read on a thread of its own, but for those <paramref name="ignoredDevices"/>
    /// lists (the value of <see cref="IgnoreVariable"/>, null for none); nothing runs before <see cref="Start"/>.
    /// </summary>
    [SupportedOSPlatform("linux")]
    public EvdevPads(string? ignoredDevices)
        : this(new LinuxEvdevHost(), new ThreadPadWorkers(), IgnoredPads.Parse(ignoredDevices))
    {
    }

    internal EvdevPads(IEvdevHost host, IPadWorkers workers, IgnoredPads skipped)
    {
        _host = host;
        _workers = workers;
        _skipped = skipped;
        _slots = new EvdevSlot[InputLimits.DefaultGamepads];
        _readers = new EvdevReader?[_slots.Length];
        _paths = new string?[_slots.Length];

        for (var slot = 0; slot < _slots.Length; slot++)
        {
            _slots[slot] = new EvdevSlot();
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

        return _slots[gamepad].TrySetRumble(Magnitude(lowFrequency), Magnitude(highFrequency));
    }

    /// <summary>Always false: evdev sets no pad light (a DualSense's is a LED class device of its own).</summary>
    public bool TrySetLight(int gamepad, float red, float green, float blue)
    {
        return false;
    }

    /// <summary>
    /// One look for pads: frees the slots of readers that ended, stops the readers of pads no longer present, and opens
    /// every pad not yet read into a free slot. The discovery thread runs it; a test calls it directly.
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

    /// <summary>A level as a motor magnitude: clamped to 0..1, rounded; not a number reads as 0.</summary>
    internal static ushort Magnitude(float level)
    {
        if (!(level > 0f))
        {
            return 0;
        }

        return (ushort)MathF.Round(MathF.Min(level, 1f) * FullMagnitude);
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

            if (path is not null && !_present.Contains(path, StringComparer.Ordinal))
            {
                _readers[slot]?.Stop();
            }
        }
    }

    private void TryOpen(string path)
    {
        if (!_host.TryDescribe(path, out var gamepad, out var description))
        {
            return;
        }

        if (!gamepad || description is null || _skipped.Contains(description.Vendor, description.Product))
        {
            _ignored.Add(path);

            return;
        }

        var slot = SlotOf(null);

        if (slot < 0)
        {
            return;
        }

        var device = _host.Open(path, description);

        if (device is null)
        {
            return;
        }

        var reader = new EvdevReader(device, new EvdevPadState(description), _slots[slot]);
        _readers[slot] = reader;
        _paths[slot] = path;
        _slots[slot].Assign(reader, description.Family);
        _workers.Start(reader);
    }

    /// <summary>The slot read from <paramref name="path"/> (a free slot for null); -1 when there is none.</summary>
    private int SlotOf(string? path)
    {
        for (var slot = 0; slot < _paths.Length; slot++)
        {
            if (string.Equals(_paths[slot], path, StringComparison.Ordinal))
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
