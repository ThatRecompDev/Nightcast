// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.Xbox.Input;

/// <summary>
/// Writes the client's reports for the input channel into a caller's buffer, little-endian
/// (docs/architecture/xhome.md#input-reports):
/// <code>
/// header   u16 kind | u32 sequence | f64 milliseconds since the writer started          14 bytes
/// metadata u8 most touch points                                                            1 byte
/// gamepad  u8 count, then per pad: u8 index | u16 buttons | i16 LX LY RX RY | u16 LT RT
///          | u32 physical physicality | u32 virtual physicality                       1 + 23 bytes
/// </code>
/// The gamepad layout was read from the web player and is not yet confirmed against a console.
/// </summary>
public sealed class InputReportWriter
{
    public const int HeaderSize = 14;
    public const int ClientMetadataSize = HeaderSize + 1;
    public const int GamepadEntrySize = 23;
    public const int GamepadReportSize = HeaderSize + 1 + GamepadEntrySize;

    private const int SequenceOffset = 2;
    private const int TimeOffset = 6;
    private const int ButtonsOffset = 1;
    private const int LeftXOffset = 3;
    private const int LeftYOffset = 5;
    private const int RightXOffset = 7;
    private const int RightYOffset = 9;
    private const int LeftTriggerOffset = 11;
    private const int RightTriggerOffset = 13;
    private const int PhysicalOffset = 15;
    private const int VirtualOffset = 19;

    private readonly TimeProvider _clock;
    private readonly long _start;
    private uint _sequence;

    public InputReportWriter(TimeProvider clock)
    {
        _clock = clock;
        _start = clock.GetTimestamp();
    }

    /// <summary>The report sent once when the channel opens; returns the bytes written.</summary>
    public int WriteClientMetadata(Span<byte> destination, byte mostTouchPoints)
    {
        WriteHeader(destination, InputReportKind.ClientMetadata);
        destination[HeaderSize] = mostTouchPoints;

        return ClientMetadataSize;
    }

    /// <summary>One pad's report; returns the bytes written (<see cref="GamepadReportSize"/>).</summary>
    [HotPath]
    public int WriteGamepad(Span<byte> destination, byte index, in XboxPadState pad)
    {
        WriteHeader(destination, InputReportKind.Gamepad);
        destination[HeaderSize] = 1;
        var entry = destination.Slice(HeaderSize + 1, GamepadEntrySize);
        entry[0] = index;

        BinaryPrimitives.WriteUInt16LittleEndian(entry[ButtonsOffset..], (ushort)pad.Buttons);
        BinaryPrimitives.WriteInt16LittleEndian(entry[LeftXOffset..], InputScales.Axis(pad.LeftX));
        BinaryPrimitives.WriteInt16LittleEndian(entry[LeftYOffset..], InputScales.Axis(pad.LeftY));
        BinaryPrimitives.WriteInt16LittleEndian(entry[RightXOffset..], InputScales.Axis(pad.RightX));
        BinaryPrimitives.WriteInt16LittleEndian(entry[RightYOffset..], InputScales.Axis(pad.RightY));

        BinaryPrimitives.WriteUInt16LittleEndian(entry[LeftTriggerOffset..], InputScales.Trigger(pad.LeftTrigger));
        BinaryPrimitives.WriteUInt16LittleEndian(entry[RightTriggerOffset..], InputScales.Trigger(pad.RightTrigger));
        BinaryPrimitives.WriteUInt32LittleEndian(entry[PhysicalOffset..], 0);
        BinaryPrimitives.WriteUInt32LittleEndian(entry[VirtualOffset..], 0);

        return GamepadReportSize;
    }

    [HotPath]
    private void WriteHeader(Span<byte> destination, InputReportKind kind)
    {
        double milliseconds = _clock.GetElapsedTime(_start).TotalMilliseconds;

        BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)kind);
        BinaryPrimitives.WriteUInt32LittleEndian(destination[SequenceOffset..], _sequence);
        BinaryPrimitives.WriteDoubleLittleEndian(destination[TimeOffset..], milliseconds);
        _sequence++;
    }
}
