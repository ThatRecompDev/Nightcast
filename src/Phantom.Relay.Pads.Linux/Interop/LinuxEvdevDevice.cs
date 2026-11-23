// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Phantom.Relay.Pads.Linux.Devices;
using Phantom.Relay.Pads.Linux.Events;
using Phantom.Relay.Pads.Linux.Mapping;

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary>
/// One open event device, read without blocking: a read waits in <c>poll</c> on the device and on an <c>eventfd</c>
/// (<see cref="Wake"/> writes to it) for at most <see cref="WaitMilliseconds"/>. Rumble is one <c>FF_RUMBLE</c> effect,
/// uploaded once and updated in place (<c>EVIOCSFF</c> with its id), played and stopped by writing <c>EV_FF</c>
/// events; it is stopped and removed when the device is closed. Its buffers are made once.
/// </summary>
[SupportedOSPlatform("linux")]
internal sealed unsafe class LinuxEvdevDevice : IEvdevDevice
{
    /// <summary>NOT DATA: the longest a read waits before the reader looks at its stop flag again.</summary>
    private const int WaitMilliseconds = 100;

    private const int Descriptors = 2;

    private readonly int _descriptor;
    private readonly int _wake;
    private readonly EvdevDescription _description;
    private readonly bool _rumble;
    private readonly byte[] _effect = new byte[EvdevRequests.EffectSize];
    private readonly byte[] _event = new byte[InputEvent.Size];
    private readonly byte[] _keys = new byte[EvdevCapabilities.KeyBytes];
    private short _effectId = RumbleEffect.NewEffect;
    private bool _disposed;

    public LinuxEvdevDevice(int descriptor, int wake, EvdevDescription description, bool rumble)
    {
        _descriptor = descriptor;
        _wake = wake;
        _description = description;
        _rumble = rumble;
    }

    public EvdevReadResult Read(Span<byte> events, out int length)
    {
        length = 0;
        var waiting = stackalloc PollDescriptor[Descriptors];
        waiting[0] = new PollDescriptor { Descriptor = _descriptor, Events = LibcMethods.PollIn };
        waiting[1] = new PollDescriptor { Descriptor = _wake, Events = LibcMethods.PollIn };
        var ready = LibcMethods.Poll(waiting, Descriptors, WaitMilliseconds);

        if (ready < 0)
        {
            return Failed();
        }

        if ((waiting[1].ReturnedEvents & LibcMethods.PollIn) != 0)
        {
            DrainWake();
        }

        if ((waiting[0].ReturnedEvents & (LibcMethods.PollError | LibcMethods.PollHangUp | LibcMethods.PollInvalid)) != 0)
        {
            return EvdevReadResult.Gone;
        }

        if ((waiting[0].ReturnedEvents & LibcMethods.PollIn) == 0)
        {
            return EvdevReadResult.Idle;
        }

        return ReadEvents(events, out length);
    }

    public void Wake()
    {
        ulong one = 1;
        _ = LibcMethods.Write(_wake, &one, sizeof(ulong));
    }

    public void Resynchronise(EvdevPadState pad)
    {
        fixed (byte* keys = _keys)
        {
            if (LibcMethods.Ioctl(_descriptor, EvdevRequests.Keys(_keys.Length), keys) >= 0)
            {
                foreach (var code in EvdevButtons.Codes)
                {
                    var held = 0;

                    if (EvdevCapabilities.Has(_keys, code))
                    {
                        held = 1;
                    }

                    pad.Apply(new InputEvent(EvdevCodes.Key, code, held));
                }
            }
        }

        for (var code = 0; code < _description.Axes.Length; code++)
        {
            if (_description.Axes[code].Present)
            {
                var axis = LinuxEvdevHost.Range(_descriptor, code);
                pad.Apply(new InputEvent(EvdevCodes.Absolute, (ushort)code, axis.Value));
            }
        }
    }

    public bool Rumble(ushort strong, ushort weak)
    {
        if (!_rumble)
        {
            return false;
        }

        RumbleEffect.Write(_effect, _effectId, strong, weak);

        fixed (byte* effect = _effect)
        {
            if (LibcMethods.Ioctl(_descriptor, EvdevRequests.Upload, effect) < 0)
            {
                return false;
            }
        }

        _effectId = RumbleEffect.Id(_effect);

        return Play(strong != 0 || weak != 0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_effectId != RumbleEffect.NewEffect)
        {
            Play(false);
            _ = LibcMethods.Ioctl(_descriptor, EvdevRequests.Remove, (void*)(nint)_effectId);
        }

        _ = LibcMethods.Close(_descriptor);
        _ = LibcMethods.Close(_wake);
    }

    private EvdevReadResult ReadEvents(Span<byte> events, out int length)
    {
        length = 0;
        var whole = events.Length - (events.Length % InputEvent.Size);
        nint read;

        fixed (byte* buffer = events)
        {
            read = LibcMethods.Read(_descriptor, buffer, (nuint)whole);
        }

        if (read < 0)
        {
            return Failed();
        }

        length = (int)read;

        return EvdevReadResult.Events;
    }

    /// <summary>An interrupted call or one that would have waited is a read without events; anything else (ENODEV) the end.</summary>
    private static EvdevReadResult Failed()
    {
        var error = Marshal.GetLastPInvokeError();

        if (error == LibcMethods.Interrupted || error == LibcMethods.WouldBlock)
        {
            return EvdevReadResult.Idle;
        }

        return EvdevReadResult.Gone;
    }

    private void DrainWake()
    {
        ulong count;
        _ = LibcMethods.Read(_wake, &count, sizeof(ulong));
    }

    private bool Play(bool play)
    {
        RumbleEffect.Playback(_effectId, play).Write(_event);

        fixed (byte* playback = _event)
        {
            return LibcMethods.Write(_descriptor, playback, InputEvent.Size) == InputEvent.Size;
        }
    }
}
