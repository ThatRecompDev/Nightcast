// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.WebRtc.H264;

/// <summary>The fixed values of H.264 NAL units (ITU-T H.264 section 7.3.1) and their RTP payload format (RFC 6184).</summary>
public static class H264Format
{
    /// <summary>The NAL unit header's type field: its low five bits.</summary>
    public const int TypeMask = 0x1F;

    /// <summary>The forbidden_zero_bit, set only by a sender that knows the NAL unit is damaged.</summary>
    public const int ForbiddenBit = 0x80;

    /// <summary>The forbidden bit and nal_ref_idc, which an FU's indicator carries for the NAL unit it rebuilds.</summary>
    public const int IndicatorMask = 0xE0;

    public const int NonIdrSlice = 1;

    public const int IdrSlice = 5;

    public const int SequenceParameterSet = 7;

    public const int PictureParameterSet = 8;

    /// <summary>The last NAL unit type sent as a single NAL unit packet; 24 to 31 are RTP's own packet types.</summary>
    public const int LastSingleNalType = 23;

    /// <summary>Single-time aggregation packet: NAL units of one access unit, each after a 16-bit size.</summary>
    public const int StapA = 24;

    /// <summary>Fragmentation unit: one NAL unit split across packets.</summary>
    public const int FuA = 28;

    public const int StapSizeLength = 2;

    /// <summary>An FU-A's indicator octet and FU header octet.</summary>
    public const int FuHeaderLength = 2;

    public const int FuStartBit = 0x80;

    public const int FuEndBit = 0x40;

    /// <summary>The Annex-B start code before every NAL unit: 00 00 00 01.</summary>
    public const uint StartCode = 0x00000001;

    public const int StartCodeLength = 4;

    /// <summary>A 4:2:0 picture of 8-bit samples holds 12 bits a pixel; no conforming access unit is larger.</summary>
    public const int RawBitsPerPixel = 12;

    public const int BitsPerByte = 8;

    /// <summary>The RTP clock of H.264 video (RFC 6184 section 8.2.1).</summary>
    public const int ClockRate = 90000;
}
