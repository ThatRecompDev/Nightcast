// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>
/// The libavutil calls the decoder makes (FFmpeg 8's public API: version.h, hwcontext.h, buffer.h, frame.h, opt.h,
/// error.h).
/// </summary>
internal static unsafe partial class AvUtilMethods
{
    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "avutil_version")]
    public static partial uint Version();

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_version_info")]
    public static partial nint VersionInfo();

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_hwdevice_ctx_create", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int CreateDevice(out nint device, int type, string? name, nint options, int flags);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_buffer_ref")]
    public static partial nint BufferRef(nint buffer);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_buffer_unref")]
    public static partial void BufferUnref(ref nint buffer);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_frame_alloc")]
    public static partial nint FrameAlloc();

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_frame_free")]
    public static partial void FrameFree(ref nint frame);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_frame_unref")]
    public static partial void FrameUnref(nint frame);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_frame_move_ref")]
    public static partial void FrameMoveRef(nint target, nint source);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_hwframe_transfer_data")]
    public static partial int TransferData(nint target, nint source, int flags);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_opt_set", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetOption(nint target, string name, string value, int searchFlags);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_opt_set_int", StringMarshalling = StringMarshalling.Utf8)]
    public static partial int SetIntOption(nint target, string name, long value, int searchFlags);

    [LibraryImport(FfmpegLibraries.AvUtil, EntryPoint = "av_strerror")]
    public static partial int ErrorText(int error, byte* buffer, nuint size);
}
