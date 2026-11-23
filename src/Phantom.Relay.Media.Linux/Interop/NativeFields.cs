// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Linux.Interop;

/// <summary>
/// Reads and writes one field of a native FFmpeg struct at its <see cref="FfmpegLayout"/> offset, and turns FFmpeg's
/// error codes into text. The struct is FFmpeg's; the caller keeps it alive.
/// </summary>
internal static unsafe class NativeFields
{
    public static int ReadInt(nint structure, int offset)
    {
        return *(int*)(structure + offset);
    }

    public static long ReadLong(nint structure, int offset)
    {
        return *(long*)(structure + offset);
    }

    public static nint ReadPointer(nint structure, int offset)
    {
        return *(nint*)(structure + offset);
    }

    public static void WriteInt(nint structure, int offset, int value)
    {
        *(int*)(structure + offset) = value;
    }

    public static void WriteLong(nint structure, int offset, long value)
    {
        *(long*)(structure + offset) = value;
    }

    public static void WritePointer(nint structure, int offset, nint value)
    {
        *(nint*)(structure + offset) = value;
    }

    /// <summary>Plane <paramref name="plane"/>'s pointer of an AVFrame.</summary>
    public static nint FramePlane(nint frame, int plane)
    {
        return ReadPointer(frame, FfmpegLayout.FrameData + (plane * sizeof(nint)));
    }

    /// <summary>Plane <paramref name="plane"/>'s pitch of an AVFrame.</summary>
    public static int FramePitch(nint frame, int plane)
    {
        return ReadInt(frame, FfmpegLayout.FrameLinesize + (plane * sizeof(int)));
    }

    /// <summary>FFmpeg's text for an error code (av_strerror), with the code.</summary>
    public static string Describe(int error)
    {
        var text = stackalloc byte[FfmpegLayout.ErrorTextBytes];

        if (AvUtilMethods.ErrorText(error, text, FfmpegLayout.ErrorTextBytes) < 0)
        {
            return $"error {error}";
        }

        return $"{Marshal.PtrToStringUTF8((nint)text)} ({error})";
    }
}
