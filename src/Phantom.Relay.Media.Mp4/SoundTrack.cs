// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>The sound's trak: its header at full volume, a 48 kHz media header, the sound handler, smhd and the Opus entry.</summary>
internal static class SoundTrack
{
    public const string HandlerName = "SoundHandler";

    /// <summary>tkhd's volume: 1.0 (8.8).</summary>
    public const int FullVolume = 0x0100;

    // smhd: balance 0 (centre) and a reserved field.
    private const int SoundHeaderBytes = 4;

    public static void Write(BoxBuffer buffer, Mp4AudioTrack track)
    {
        var trak = buffer.Begin(Mp4BoxType.Track);
        InitSegment.WriteTrackHeader(buffer, InitSegment.AudioTrackId, FullVolume, 0, 0);
        var media = buffer.Begin(Mp4BoxType.Media);
        InitSegment.WriteMediaHeaders(buffer, Mp4AudioTrack.Timescale, Mp4BoxType.HandlerSound, HandlerName);

        var information = buffer.Begin(Mp4BoxType.MediaInformation);
        var header = buffer.BeginFull(Mp4BoxType.SoundMediaHeader, 0, 0);
        buffer.Zeros(SoundHeaderBytes);
        buffer.End(header);
        InitSegment.WriteDataAndSamples(buffer, entries => OpusEntry.Write(entries, track));

        buffer.End(information);
        buffer.End(media);
        buffer.End(trak);
    }
}
