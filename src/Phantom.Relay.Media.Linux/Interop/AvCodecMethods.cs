// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>The libavcodec calls the decoder makes (FFmpeg 8's public API, libavcodec/avcodec.h and packet.h).</summary>
internal static unsafe partial class AvCodecMethods
{
    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_version")]
    public static partial uint Version();

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_license")]
    public static partial nint License();

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_find_decoder")]
    public static partial nint FindDecoder(int codecId);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_alloc_context3")]
    public static partial nint AllocContext(nint codec);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_open2")]
    public static partial int Open(nint context, nint codec, nint options);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_free_context")]
    public static partial void FreeContext(ref nint context);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_send_packet")]
    public static partial int SendPacket(nint context, nint packet);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_receive_frame")]
    public static partial int ReceiveFrame(nint context, nint frame);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "avcodec_flush_buffers")]
    public static partial void FlushBuffers(nint context);

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "av_packet_alloc")]
    public static partial nint PacketAlloc();

    [LibraryImport(FfmpegLibraries.AvCodec, EntryPoint = "av_packet_free")]
    public static partial void PacketFree(ref nint packet);
}
