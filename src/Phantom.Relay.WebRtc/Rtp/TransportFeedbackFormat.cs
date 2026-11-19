// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.Rtp;

/// <summary>
/// The fixed values of transport-wide congestion control (draft-holmer-rmcat-transport-wide-cc-extensions-01): the RTP
/// header extension that numbers every packet across the transport (section 2), and the RTCP transport feedback
/// (RTPFB, FMT=15, section 3.1) that tells the sender when each of them arrived.
/// </summary>
public static class TransportFeedbackFormat
{
    /// <summary>The header extension's URI, as an a=extmap line names it.</summary>
    public const string ExtensionUri = "http://www.ietf.org/id/draft-holmer-rmcat-transport-wide-cc-extensions-01";

    /// <summary>The extension's data: the 16-bit transport-wide sequence number.</summary>
    public const int ExtensionLength = 2;

    /// <summary>The fixed part: the feedback header, the base sequence and status count, the reference time and feedback count.</summary>
    public const int HeaderLength = 20;

    public const int BaseSequenceOffset = 12;

    public const int StatusCountOffset = 14;

    /// <summary>The reference time (24 bits, 64 ms units) shares a word with the 8-bit feedback packet count.</summary>
    public const int ReferenceTimeOffset = 16;

    public const int ReferenceTimeMask = 0xFFFFFF;

    public const int ReferenceTimeShift = 8;

    /// <summary>A receive delta counts 250 µs: this many <see cref="TimeSpan"/> ticks.</summary>
    public const long DeltaTicks = 2500;

    /// <summary>The reference time's unit, 64 ms, is 256 receive-delta units.</summary>
    public const int DeltasPerReference = 256;

    /// <summary>The status symbols (section 3.1.1): not received, received with a one-octet delta, with a two-octet one.</summary>
    public const byte NotReceived = 0;

    public const byte SmallDelta = 1;

    public const byte LargeDelta = 2;

    /// <summary>A small delta is one unsigned octet: 0 to 63.75 ms.</summary>
    public const int SmallDeltaMax = 255;

    public const int SmallDeltaLength = 1;

    /// <summary>A large delta is a signed 16-bit count of 250 µs.</summary>
    public const int LargeDeltaLength = 2;

    /// <summary>Each packet status chunk is 16 bits.</summary>
    public const int ChunkLength = 2;

    /// <summary>A run-length chunk: T=0, the 2-bit symbol, a 13-bit run.</summary>
    public const int RunSymbolShift = 13;

    public const int MaxRunLength = 8191;

    /// <summary>A status vector chunk: T=1, then S=0 for 14 one-bit symbols or S=1 for 7 two-bit ones.</summary>
    public const int VectorChunk = 0x8000;

    public const int TwoBitVectorChunk = 0xC000;

    public const int OneBitSymbols = 14;

    public const int TwoBitSymbols = 7;

    public const int TwoBitSymbolWidth = 2;

    /// <summary>The deltas end with zero padding to a 32-bit boundary.</summary>
    public const int MaxPadding = RtcpFormat.WordLength - 1;

    /// <summary>
    /// The longest a feedback of <paramref name="statuses"/> statuses and <paramref name="deltaBytes"/> octets of deltas
    /// can be: every chunk but the last covers seven statuses at least.
    /// </summary>
    public static int MaxLength(int statuses, int deltaBytes)
    {
        int chunks = (statuses + TwoBitSymbols - 1) / TwoBitSymbols;

        return HeaderLength + (chunks * ChunkLength) + deltaBytes + MaxPadding;
    }
}
