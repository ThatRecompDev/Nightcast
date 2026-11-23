// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Phantom.Engine.Core;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Linux.Interop;
using Phantom.Relay.Media.Windows;

namespace Phantom.Relay.Media.Linux;

/// <summary>
/// H.264 decoded on the GPU through VA-API on Linux (SteamOS's Mesa radeonsi on the ROG Ally), by FFmpeg's libavcodec
/// with its VA-API hwaccel, behind Phantom.Engine's <see cref="IVideoDecoder"/>, so a head swaps it in for the engine's
/// openh264 <c>H264Decoder</c>. One Annex-B access unit in at a time, with its timestamp; one NV12 picture out, read
/// back from the VA surface and copied into the <see cref="VideoFrame"/>'s Y, Cb and Cr planes. Used from one thread at
/// a time (the stream's decode worker).
/// </summary>
/// <remarks>
/// <para><b>Why.</b> As on Windows (MediaFoundationH264Decoder): the console streams H.264 Main 4.2 at up to 2560 x 1440
/// and 60 fps, and openh264 on the CPU costs milliseconds a picture with key-frame spikes, all of it latency. A
/// handheld's CPU is also its battery and its thermal headroom; the GPU's fixed-function decoder (VCN on RDNA3) decodes
/// in near-constant time.</para>
/// <para><b>Low delay.</b> The context opens with AV_CODEC_FLAG_LOW_DELAY and one thread (no frame threading, which
/// would hold a picture back per thread), so each access unit's picture comes out of its own
/// <c>avcodec_send_packet</c>/<c>avcodec_receive_frame</c>: this decoder never <see cref="Reorders"/>. The console's
/// stream has no B-frames.</para>
/// <para><b>Format.</b> The get_format callback (<see cref="FormatChoice"/>) takes VA-API surfaces when the hwaccel
/// offers them; when it cannot take the stream libavcodec decodes it in software into three planes, which are copied
/// as they are, and <see cref="IsHardwareAccelerated"/> turns false.</para>
/// <para><b>Timing.</b> <see cref="LastDecodeDuration"/> is the send and receive; the GPU decodes asynchronously, so the
/// wait for it lands in <see cref="LastCopyDuration"/>, in av_hwframe_transfer_data (vaSyncSurface, then the read of
/// the surface into system memory) and the NV12 split.</para>
/// <para><b>Native layout.</b> The few struct fields it touches are mirrored by offset for FFmpeg 8
/// (<see cref="FfmpegLayout"/>); another major version is refused at start. The libraries are an LGPL build of
/// FFmpeg's own, loaded at run time from the app's folder or the system's (docs/architecture/linux-decode.md).</para>
/// <para><b>Allocation.</b> Nothing managed per picture; the packet and both frames are made once. libavcodec copies
/// each packet (it is not reference-counted) and the read-back allocates its system-memory frame from FFmpeg's
/// pools.</para>
/// </remarks>
[SupportedOSPlatform("linux")]
public sealed unsafe class VaapiH264Decoder : IVideoDecoder
{
    /// <summary>Matroska's codec ID for H.264, as the engine's H264Decoder takes.</summary>
    public const string AvcCodecId = "V_MPEG4/ISO/AVC";

    private const int LumaPlane = 0;
    private const int ChromaPlane = 1;
    private const int CrPlane = 2;

    // One thread and no threading kind (neither frame nor slice threads): a picture per access unit, at once.
    private const int SingleThread = 1;
    private const int NoThreading = 0;

    private readonly TimeProvider _time;
    private readonly VaapiDevice _device;
    private nint _context;
    private nint _packet;
    private nint _frame;
    private nint _incoming;
    private nint _readBack;
    private bool _ready;
    private VideoColourSpace? _space;
    private VideoRange _range;
    private bool _disposed;

    private VaapiH264Decoder(TimeProvider time, VaapiDevice device, nint context)
    {
        _time = time;
        _device = device;
        _context = context;

        // The packet carries the caller's bytes; the frames are the newest picture, the one being received, and the
        // picture read back into system memory.
        _packet = AvCodecMethods.PacketAlloc();
        _frame = AvUtilMethods.FrameAlloc();
        _incoming = AvUtilMethods.FrameAlloc();
        _readBack = AvUtilMethods.FrameAlloc();
    }

    public string CodecId => AvcCodecId;

    /// <summary>Never: in low-delay mode each picture comes out of the decode of its own access unit.</summary>
    public bool Reorders => false;

    public string LastError { get; private set; } = string.Empty;

    /// <summary>Whether the last picture was decoded on the GPU (VA-API surfaces), not by libavcodec's software path.</summary>
    public bool IsHardwareAccelerated { get; private set; } = true;

    /// <summary>The VA driver's own name (Mesa's for radeonsi on SteamOS).</summary>
    public string Driver => _device.Driver;

    /// <summary>The DRM render node the decoder runs on.</summary>
    public string RenderNode => _device.RenderNode;

    /// <summary>FFmpeg's version, as the loaded libavutil reports it.</summary>
    public string FfmpegVersion { get; private init; } = string.Empty;

    /// <summary>How long the last <see cref="Decode(ReadOnlySpan{byte}, long)"/> took.</summary>
    public TimeSpan LastDecodeDuration { get; private set; }

    /// <summary>How long the last <see cref="TakePicture(VideoFrame, out long)"/> took to read the picture back and split it.</summary>
    public TimeSpan LastCopyDuration { get; private set; }

    /// <summary>
    /// A decoder on VA-API on <paramref name="renderNode"/> (the first render node when null); null and why when
    /// FFmpeg's libraries are missing or of another major version, or VA-API does not start.
    /// </summary>
    public static VaapiH264Decoder? TryOpen(TimeProvider time, string? renderNode, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(time);

        if (!TryLoadLibraries(out problem))
        {
            return null;
        }

        var codec = AvCodecMethods.FindDecoder(FfmpegLayout.CodecIdH264);

        if (codec == 0)
        {
            problem = "this libavcodec has no H.264 decoder";

            return null;
        }

        var device = VaapiDevice.TryCreate(renderNode ?? VaapiDevice.DefaultRenderNode, out problem);

        if (device is null)
        {
            return null;
        }

        var context = TryOpenContext(codec, device, out problem);

        if (context == 0)
        {
            device.Dispose();

            return null;
        }

        var version = Marshal.PtrToStringUTF8(AvUtilMethods.VersionInfo()) ?? string.Empty;

        return new VaapiH264Decoder(time, device, context) { FfmpegVersion = version };
    }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha)
    {
        return Decode(frame, 0);
    }

    public VideoDecodeResult Decode(ReadOnlySpan<byte> frame, ReadOnlySpan<byte> alpha, long timestampNanoseconds)
    {
        return Decode(frame, timestampNanoseconds);
    }

    /// <summary>Decodes one access unit (Annex-B) shown at <paramref name="timestampNanoseconds"/>; its picture is ready after.</summary>
    [HotPath]
    public VideoDecodeResult Decode(ReadOnlySpan<byte> accessUnit, long timestampNanoseconds)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var started = _time.GetTimestamp();
        _ready = false;

        if (accessUnit.IsEmpty)
        {
            return VideoDecodeResult.Damaged;
        }

        NoteColour(accessUnit);
        var result = Send(accessUnit, timestampNanoseconds);

        if (result == VideoDecodeResult.Picture)
        {
            result = Receive();
        }

        LastDecodeDuration = _time.GetElapsedTime(started);

        return result;
    }

    /// <summary>Copies the picture the last decode gave into <paramref name="target"/> with its timestamp; false when none.</summary>
    [HotPath]
    public bool TakePicture(VideoFrame target, out long timestampNanoseconds)
    {
        ArgumentNullException.ThrowIfNull(target);
        ObjectDisposedException.ThrowIf(_disposed, this);

        timestampNanoseconds = 0;

        if (!_ready)
        {
            return false;
        }

        var started = _time.GetTimestamp();
        var copied = CopyPicture(target);
        LastCopyDuration = _time.GetElapsedTime(started);
        _ready = false;

        if (!copied)
        {
            return false;
        }

        timestampNanoseconds = NativeFields.ReadLong(_frame, FfmpegLayout.FramePts);
        target.TimestampNanoseconds = timestampNanoseconds;

        // The stream's colour as its last sequence parameter set stated it, drawn as BT.709 limited when it said none.
        target.StreamSpace = _space;
        target.StreamRange = _range;
        target.Colour = H264StreamColour.Settle(_space, _range);

        return true;
    }

    public bool TakePicture(VideoFrame target)
    {
        return TakePicture(target, out _);
    }

    /// <summary>Forgets the stream, for a seek or a lost packet: the next access unit must be a key frame.</summary>
    public void Flush()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        AvCodecMethods.FlushBuffers(_context);
        AvUtilMethods.FrameUnref(_frame);
        _ready = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The frames and the packet, then the context (with its reference to the device), then the device.
        AvUtilMethods.FrameFree(ref _readBack);
        AvUtilMethods.FrameFree(ref _incoming);
        AvUtilMethods.FrameFree(ref _frame);
        AvCodecMethods.PacketFree(ref _packet);
        AvCodecMethods.FreeContext(ref _context);
        _device.Dispose();
    }

    // The libraries, of the major versions the layout is for.
    private static bool TryLoadLibraries(out string? problem)
    {
        if (!FfmpegLibraries.TryLoad(out problem))
        {
            return false;
        }

        problem = FfmpegLibraries.CheckVersions(AvCodecMethods.Version(), AvUtilMethods.Version());

        return problem is null;
    }

    // The H.264 decoder's context on the device: VA-API surfaces chosen, low delay, one thread; 0 and why on failure.
    private static nint TryOpenContext(nint codec, VaapiDevice device, out string? problem)
    {
        var context = AvCodecMethods.AllocContext(codec);

        if (context == 0)
        {
            problem = "libavcodec could not make a decoder context";

            return 0;
        }

        delegate* unmanaged[Cdecl]<nint, int*, int> choose = &ChooseFormat;
        NativeFields.WritePointer(context, FfmpegLayout.CodecContextGetFormat, (nint)choose);
        NativeFields.WritePointer(context, FfmpegLayout.CodecContextHwDevice, AvUtilMethods.BufferRef(device.Buffer));

        var result = AvUtilMethods.SetOption(context, "flags", "+low_delay", 0);
        result = Math.Min(result, AvUtilMethods.SetIntOption(context, "threads", SingleThread, 0));
        result = Math.Min(result, AvUtilMethods.SetIntOption(context, "thread_type", NoThreading, 0));

        if (result >= 0)
        {
            result = AvCodecMethods.Open(context, codec, 0);
        }

        if (result < 0)
        {
            problem = $"libavcodec's H.264 decoder did not open on VA-API: {NativeFields.Describe(result)}";
            AvCodecMethods.FreeContext(ref context);

            return 0;
        }

        problem = null;

        return context;
    }

    // libavcodec's get_format: the formats it can give for the stream, ended by AV_PIX_FMT_NONE.
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static int ChooseFormat(nint context, int* offered)
    {
        var count = 0;

        while (offered[count] != FfmpegLayout.PixelFormatNone)
        {
            count++;
        }

        return FormatChoice.Choose(new ReadOnlySpan<int>(offered, count));
    }

    // The stream's colour, from a sequence parameter set the access unit carries (a key frame's).
    [HotPath]
    private void NoteColour(ReadOnlySpan<byte> accessUnit)
    {
        if (AnnexBUnits.TryFind(accessUnit, AnnexBUnits.SequenceParameterSetType, out var set)
            && H264StreamColour.TryRead(set, out var space, out var range))
        {
            _space = space;
            _range = range;
        }
    }

    // The access unit to libavcodec, which copies it (the packet is not reference-counted). Picture means it took it.
    [HotPath]
    private VideoDecodeResult Send(ReadOnlySpan<byte> accessUnit, long timestamp)
    {
        int result;

        fixed (byte* data = accessUnit)
        {
            NativeFields.WritePointer(_packet, FfmpegLayout.PacketData, (nint)data);
            NativeFields.WriteInt(_packet, FfmpegLayout.PacketSize, accessUnit.Length);
            NativeFields.WriteLong(_packet, FfmpegLayout.PacketPts, timestamp);
            result = AvCodecMethods.SendPacket(_context, _packet);

            if (result == FfmpegLayout.ErrorAgain)
            {
                // A picture was still waiting: take it, then the decoder takes the access unit.
                _ = Receive();
                result = AvCodecMethods.SendPacket(_context, _packet);
            }

            NativeFields.WritePointer(_packet, FfmpegLayout.PacketData, 0);
            NativeFields.WriteInt(_packet, FfmpegLayout.PacketSize, 0);
        }

        if (result < 0)
        {
            return Fail("avcodec_send_packet", result);
        }

        return VideoDecodeResult.Picture;
    }

    // Every picture libavcodec has ready; the newest is kept. Receiving unreferences its target first, so each comes
    // into the incoming frame and moves over.
    [HotPath]
    private VideoDecodeResult Receive()
    {
        while (true)
        {
            var result = AvCodecMethods.ReceiveFrame(_context, _incoming);

            if (result == FfmpegLayout.ErrorAgain || result == FfmpegLayout.ErrorEndOfFile)
            {
                break;
            }

            if (result < 0)
            {
                return Fail("avcodec_receive_frame", result);
            }

            AvUtilMethods.FrameUnref(_frame);
            AvUtilMethods.FrameMoveRef(_frame, _incoming);
            _ready = true;
        }

        if (_ready)
        {
            return VideoDecodeResult.Picture;
        }

        return VideoDecodeResult.Hidden;
    }

    // A VA-API surface is read back into NV12 first; a software picture is copied as it is.
    [HotPath]
    private bool CopyPicture(VideoFrame target)
    {
        var format = NativeFields.ReadInt(_frame, FfmpegLayout.FrameFormat);
        var width = NativeFields.ReadInt(_frame, FfmpegLayout.FrameWidth);
        var height = NativeFields.ReadInt(_frame, FfmpegLayout.FrameHeight);

        if (format == FfmpegLayout.PixelFormatVaapi)
        {
            IsHardwareAccelerated = true;

            return ReadBack(width, height, target);
        }

        if (FormatChoice.IsPlanar420(format))
        {
            IsHardwareAccelerated = false;
            CopyPlanar(_frame, width, height, target);

            return true;
        }

        return Unread(format);
    }

    [HotPath]
    private bool ReadBack(int width, int height, VideoFrame target)
    {
        AvUtilMethods.FrameUnref(_readBack);
        NativeFields.WriteInt(_readBack, FfmpegLayout.FrameFormat, FfmpegLayout.PixelFormatNv12);
        var result = AvUtilMethods.TransferData(_readBack, _frame, 0);

        if (result < 0)
        {
            _ = Fail("av_hwframe_transfer_data", result);

            return false;
        }

        var lumaPitch = NativeFields.FramePitch(_readBack, LumaPlane);
        var chromaPitch = NativeFields.FramePitch(_readBack, ChromaPlane);
        var luma = new ReadOnlySpan<byte>((void*)NativeFields.FramePlane(_readBack, LumaPlane), PlaneCopies.Extent(lumaPitch, width, height));
        var chromaBytes = PlaneCopies.Extent(chromaPitch, PlaneCopies.Nv12ChromaRow(width), PlaneCopies.ChromaRows(height));
        var chroma = new ReadOnlySpan<byte>((void*)NativeFields.FramePlane(_readBack, ChromaPlane), chromaBytes);
        PlaneCopies.CopyNv12(luma, lumaPitch, chroma, chromaPitch, width, height, target);

        return true;
    }

    [HotPath]
    private static void CopyPlanar(nint frame, int width, int height, VideoFrame target)
    {
        var lumaPitch = NativeFields.FramePitch(frame, LumaPlane);
        var chromaPitch = NativeFields.FramePitch(frame, ChromaPlane);
        var chromaBytes = PlaneCopies.Extent(chromaPitch, PlaneCopies.ChromaColumns(width), PlaneCopies.ChromaRows(height));

        var luma = new ReadOnlySpan<byte>((void*)NativeFields.FramePlane(frame, LumaPlane), PlaneCopies.Extent(lumaPitch, width, height));
        var cb = new ReadOnlySpan<byte>((void*)NativeFields.FramePlane(frame, ChromaPlane), chromaBytes);
        var cr = new ReadOnlySpan<byte>((void*)NativeFields.FramePlane(frame, CrPlane), chromaBytes);
        PlaneCopies.CopyPlanar(luma, lumaPitch, cb, cr, chromaPitch, width, height, target);
    }

    private bool Unread(int format)
    {
        LastError = $"libavcodec gave a picture in format {format}, neither VA-API nor 4:2:0 planes";

        return false;
    }

    private VideoDecodeResult Fail(string step, int result)
    {
        LastError = $"libavcodec's H.264 decoder failed at {step}: {NativeFields.Describe(result)}";

        return VideoDecodeResult.Damaged;
    }
}
