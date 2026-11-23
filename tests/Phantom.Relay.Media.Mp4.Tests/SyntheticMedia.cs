// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>
/// Hand-made stream pieces for the writer's tests, never a capture: a High-profile SPS and a PPS shaped like an
/// encoder's (1280x720, level 3.1), access units in Annex-B form, and Opus packets (TOC bytes of RFC 6716 3.1 and a
/// few payload bytes; the writer reads only the TOC).
/// </summary>
public static class SyntheticMedia
{
    /// <summary>An SPS: header 0x67, profile_idc 100 (High), constraint flags 0, level_idc 31, then coded fields.</summary>
    public static readonly byte[] Sps = [0x67, 0x64, 0x00, 0x1F, 0xAC, 0xD9, 0x40, 0x50, 0x05, 0xBB, 0x01, 0x10, 0x00, 0x00, 0x03, 0x00, 0x10, 0x00, 0x00, 0x03, 0x03, 0xC0, 0xF1, 0x83, 0x19, 0x60];

    /// <summary>A PPS: header 0x68 and its coded fields.</summary>
    public static readonly byte[] Pps = [0x68, 0xEB, 0xE3, 0xCB, 0x22, 0xC0];

    /// <summary>Config 30 (CELT, fullband, 10 ms), stereo, one frame: 480 samples.</summary>
    public const byte Toc10Ms = 0xF4;

    /// <summary>Config 31 (CELT, fullband, 20 ms), stereo, one frame: 960 samples.</summary>
    public const byte Toc20Ms = 0xFC;

    /// <summary>A key frame: an access unit delimiter, the SPS and PPS (4-byte start codes), then an IDR slice (3-byte).</summary>
    public static byte[] KeyUnit(byte marker)
    {
        return [0, 0, 0, 1, 0x09, 0x10, 0, 0, 0, 1, .. Sps, 0, 0, 0, 1, .. Pps, 0, 0, 1, 0x65, 0x88, 0x84, marker, 0x21];
    }

    /// <summary>A frame that depends on others: one non-IDR slice.</summary>
    public static byte[] DeltaUnit(byte marker)
    {
        return [0, 0, 0, 1, 0x41, 0x9A, marker, 0x05, 0x10];
    }

    /// <summary>An Opus packet: <paramref name="toc"/> and three payload bytes ending in <paramref name="marker"/>.</summary>
    public static byte[] Packet(byte toc, byte marker)
    {
        return [toc, 0x0B, 0xE4, marker];
    }

    /// <summary>The video track of the SPS and PPS above.</summary>
    public static Mp4VideoTrack Video()
    {
        return new Mp4VideoTrack(1280, 720, Sps, Pps);
    }
}
