// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Sctp;

/// <summary>
/// The SCTP wire format WebRTC uses (RFC 9260, RFC 3758's FORWARD TSN, RFC 8261's encapsulation in DTLS): the common
/// header, the chunk and parameter headers, and the chunk and parameter types. Every field is big-endian except the
/// checksum, which is stored little-endian as the CRC-32C register leaves it.
/// </summary>
public static class SctpFormat
{
    /// <summary>The port both ends use for the data channels (RFC 8841's default sctp-port).</summary>
    public const ushort WebRtcPort = 5000;

    /// <summary>Source port, destination port, verification tag and checksum.</summary>
    public const int CommonHeaderLength = 12;

    public const int DestinationPortOffset = 2;
    public const int VerificationTagOffset = 4;
    public const int ChecksumOffset = 8;

    /// <summary>Type, flags and length, ahead of every chunk's value.</summary>
    public const int ChunkHeaderLength = 4;

    /// <summary>Type and length, ahead of every parameter's value.</summary>
    public const int ParameterHeaderLength = 4;

    /// <summary>Chunks and parameters are padded to a multiple of this.</summary>
    public const int Alignment = 4;

    public const byte Data = 0;
    public const byte Init = 1;
    public const byte InitAck = 2;
    public const byte Sack = 3;
    public const byte Heartbeat = 4;
    public const byte HeartbeatAck = 5;
    public const byte Abort = 6;
    public const byte Shutdown = 7;
    public const byte ShutdownAck = 8;
    public const byte Error = 9;
    public const byte CookieEcho = 10;
    public const byte CookieAck = 11;
    public const byte ShutdownComplete = 14;

    /// <summary>RFC 3758: moves the receiver's cumulative TSN past abandoned messages.</summary>
    public const byte ForwardTsn = 192;

    /// <summary>A chunk type with this bit set is skipped when unknown; without it the rest of the packet is dropped.</summary>
    public const byte SkipUnknownBit = 0x80;

    /// <summary>DATA: TSN, stream identifier, stream sequence number and payload protocol identifier.</summary>
    public const int DataFixedLength = 12;

    /// <summary>DATA flag: the last fragment of a message.</summary>
    public const byte EndFlag = 0x01;

    /// <summary>DATA flag: the first fragment of a message.</summary>
    public const byte BeginningFlag = 0x02;

    /// <summary>DATA flag: delivered as soon as it is whole, outside its stream's order.</summary>
    public const byte UnorderedFlag = 0x04;

    /// <summary>ABORT and SHUTDOWN COMPLETE: the verification tag is the sender's own, as it had no peer tag.</summary>
    public const byte TagReflectedFlag = 0x01;

    /// <summary>INIT and INIT ACK: initiate tag, receiver window, outbound streams, inbound streams, initial TSN.</summary>
    public const int InitFixedLength = 16;

    /// <summary>SACK: cumulative TSN, receiver window, gap block count and duplicate count.</summary>
    public const int SackFixedLength = 12;

    /// <summary>A gap block's start and end offsets from the cumulative TSN.</summary>
    public const int GapBlockLength = 4;

    public const int DuplicateTsnLength = 4;

    /// <summary>FORWARD TSN: the new cumulative TSN, then a stream identifier and sequence number per ordered stream.</summary>
    public const int ForwardTsnFixedLength = 4;

    public const int ForwardTsnStreamLength = 4;

    /// <summary>SHUTDOWN: the cumulative TSN its sender has received.</summary>
    public const int ShutdownLength = 4;

    public const ushort HeartbeatInfo = 1;
    public const ushort StateCookie = 7;

    /// <summary>RFC 3758: the sender understands FORWARD TSN (partial reliability).</summary>
    public const ushort ForwardTsnSupported = 0xC000;

    /// <summary>RFC 5061: the chunk types beyond RFC 9260 the sender understands, one byte each.</summary>
    public const ushort SupportedExtensions = 0x8008;
}
