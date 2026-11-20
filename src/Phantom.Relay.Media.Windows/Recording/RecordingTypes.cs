// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows.Recording;

/// <summary>The media types and attribute stores a recording's sink writer is set up with.</summary>
internal static class RecordingTypes
{
    /// <summary>
    /// A progressive video type of <paramref name="subtype"/> at <paramref name="width"/> x <paramref name="height"/>,
    /// <paramref name="framesPerSecond"/> frames a second, square pixels; the caller releases both the pointer and the
    /// wrapper (also on failure).
    /// </summary>
    public static int TryCreate(ComWrappers wrappers, in Guid subtype, int width, int height, int framesPerSecond, out nint type, out IMFAttributes? attributes)
    {
        var result = MediaFoundationTypes.TryCreateVideo(wrappers, in subtype, out type, out attributes);

        if (ComObjects.Failed(result) || attributes is null)
        {
            return result;
        }

        result = attributes.SetUInt64(in MediaFoundationIds.FrameSize, MediaFoundationTypes.Pack((uint)width, (uint)height));
        result = Next(result, attributes.SetUInt64(in MediaFoundationIds.FrameRate, MediaFoundationTypes.Pack((uint)framesPerSecond, 1)));
        result = Next(result, attributes.SetUInt64(in MediaFoundationIds.PixelAspectRatio, MediaFoundationTypes.Pack(1, 1)));

        return Next(result, attributes.SetUInt32(in MediaFoundationIds.InterlaceMode, MediaFoundationValues.Progressive));
    }

    /// <summary>States BT.709 in limited range, the colour the stream's pictures are drawn with, so players read them alike.</summary>
    public static int DescribeBt709(IMFAttributes type)
    {
        var result = type.SetUInt32(in MediaFoundationIds.NominalRange, MediaFoundationValues.LimitedRange);
        result = Next(result, type.SetUInt32(in MediaFoundationIds.YuvMatrix, MediaFoundationValues.Bt709Matrix));
        result = Next(result, type.SetUInt32(in MediaFoundationIds.VideoPrimaries, MediaFoundationValues.Bt709Primaries));

        return Next(result, type.SetUInt32(in MediaFoundationIds.TransferFunction, MediaFoundationValues.Bt709Transfer));
    }

    /// <summary>An empty attribute store; the caller releases both the pointer and the wrapper.</summary>
    public static int TryCreateAttributes(ComWrappers wrappers, out nint store, out IMFAttributes? attributes)
    {
        attributes = null;
        var result = NativeMethods.MFCreateAttributes(out store, 1);

        if (!ComObjects.Failed(result))
        {
            attributes = ComObjects.Wrap<IMFAttributes>(wrappers, store);
        }

        return result;
    }

    /// <summary>
    /// The first failure of two steps, else the second's result. Both steps run (the arguments are evaluated first), so it
    /// joins only steps that are harmless after a failure, such as setting attributes.
    /// </summary>
    public static int Next(int before, int result)
    {
        if (ComObjects.Failed(before))
        {
            return before;
        }

        return result;
    }
}
