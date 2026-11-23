// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Phantom.Engine.Media.Video;
using Phantom.Relay.Media.Linux;

namespace Phantom.Relay.Tools.DecodeProbe;

/// <summary>
/// Decodes an H.264 sample (Annex-B with access unit delimiters; never a capture of a real session) with
/// <see cref="VaapiH264Decoder"/> the way the stream's decode worker does, one access unit and its picture at a time,
/// and prints each picture's decode and read-back time, then their averages and worst cases. With a reference decode
/// (<see cref="ReferencePictures"/>) it also says how far each picture is from it.
/// <code>
/// Phantom.Relay.Tools.DecodeProbe sample.h264 [--reference reference.yuv] [--device /dev/dri/renderD128] [--quiet]
/// </code>
/// </summary>
public static class DecodeProbeProgram
{
    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int Usage = 2;

    private const string ReferenceArgument = "--reference";
    private const string DeviceArgument = "--device";
    private const string QuietArgument = "--quiet";

    public static int Main(string[] args)
    {
        if (args.Length == 0 || !OperatingSystem.IsLinux())
        {
            Console.WriteLine($"On Linux: DecodeProbe <sample.h264> [{ReferenceArgument} <reference.yuv>] [{DeviceArgument} <render node>] [{QuietArgument}]");

            return Usage;
        }

        using var decoder = VaapiH264Decoder.TryOpen(TimeProvider.System, Option(args, DeviceArgument), out var problem);

        if (decoder is null)
        {
            Console.WriteLine($"No VA-API decoder: {problem}");

            return Failed;
        }

        Console.WriteLine($"FFmpeg {decoder.FfmpegVersion}, VA-API on {decoder.RenderNode}: {decoder.Driver}");

        var reference = Option(args, ReferenceArgument);
        var pictures = reference is null ? null : new ReferencePictures(File.ReadAllBytes(reference));

        return Run(decoder, File.ReadAllBytes(args[0]), pictures, args.Contains(QuietArgument));
    }

    [SupportedOSPlatform("linux")]
    private static int Run(VaapiH264Decoder decoder, byte[] sample, ReferencePictures? reference, bool quiet)
    {
        var units = AccessUnitSplitter.Split(sample);
        var frame = new VideoFrame();
        var decode = new TimingTally();
        var readBack = new TimingTally();
        var worstDifference = 0;
        var shown = 0;

        foreach (var range in units)
        {
            var result = decoder.Decode(sample.AsSpan(range), ReadOnlySpan<byte>.Empty, shown);

            if (result != VideoDecodeResult.Picture || !decoder.TakePicture(frame, out _))
            {
                Console.WriteLine($"Access unit {shown}: {result} {decoder.LastError}");

                return Failed;
            }

            decode.Add(shown, decoder.LastDecodeDuration);
            readBack.Add(shown, decoder.LastCopyDuration);
            var difference = reference?.Difference(shown, frame) ?? -1;
            worstDifference = Math.Max(worstDifference, difference);
            Report(quiet, shown, range, sample.Length, decoder, difference);

            shown++;
        }

        Console.WriteLine($"{shown} pictures, {frame.Width} x {frame.Height}, on the GPU: {decoder.IsHardwareAccelerated}");
        Console.WriteLine($"Decode:    {decode.Describe()}");
        Console.WriteLine($"Read-back: {readBack.Describe()}");

        if (reference is not null)
        {
            Console.WriteLine($"Largest difference from the reference: {worstDifference}");
        }

        return Succeeded;
    }

    [SupportedOSPlatform("linux")]
    private static void Report(bool quiet, int index, Range range, int length, VaapiH264Decoder decoder, int difference)
    {
        if (quiet)
        {
            return;
        }

        var bytes = range.GetOffsetAndLength(length).Length;
        Console.WriteLine($"{index}: {bytes} bytes, decode {decoder.LastDecodeDuration.TotalMilliseconds:F2} ms, read-back {decoder.LastCopyDuration.TotalMilliseconds:F2} ms, difference {difference}");
    }

    private static string? Option(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);

        if (index < 0 || index + 1 >= args.Length)
        {
            return null;
        }

        return args[index + 1];
    }
}
