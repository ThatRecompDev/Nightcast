// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>Media Foundation video media types: made for a subtype, and an NV12 output type's sizes read back.</summary>
internal static unsafe class MediaFoundationTypes
{
    private const int HighBits = 32;

    // MFVideoArea (mfobjects.h): two MFOffsets of four bytes, then a SIZE's cx and cy.
    private const int ApertureBytes = 16;
    private const int ApertureWidthOffset = 8;
    private const int ApertureHeightOffset = 12;

    /// <summary>Two 32-bit values in one attribute (MF_MT_FRAME_SIZE, MF_MT_FRAME_RATE): the first high.</summary>
    public static ulong Pack(uint high, uint low)
    {
        return ((ulong)high << HighBits) | low;
    }

    /// <summary>A video media type of <paramref name="subtype"/>; the caller releases both the pointer and the wrapper.</summary>
    public static int TryCreateVideo(ComWrappers wrappers, in Guid subtype, out nint type, out IMFAttributes? attributes)
    {
        attributes = null;
        var result = NativeMethods.MFCreateMediaType(out type);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        attributes = ComObjects.Wrap<IMFAttributes>(wrappers, type);
        result = attributes.SetGuid(in MediaFoundationIds.MajorType, in MediaFoundationIds.VideoMajorType);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        return attributes.SetGuid(in MediaFoundationIds.Subtype, in subtype);
    }

    /// <summary>Whether a media type is of <paramref name="subtype"/>.</summary>
    public static bool IsSubtype(IMFAttributes type, in Guid subtype)
    {
        return !ComObjects.Failed(type.GetGuid(in MediaFoundationIds.Subtype, out var actual)) && actual == subtype;
    }

    /// <summary>An NV12 type's coded size, stride (the width when it states none) and display aperture.</summary>
    public static bool TryReadNv12(IMFAttributes type, out Nv12Format format)
    {
        format = default;

        if (ComObjects.Failed(type.GetUInt64(in MediaFoundationIds.FrameSize, out var size)))
        {
            return false;
        }

        var width = (int)(size >> HighBits);
        var height = (int)(uint)size;
        var codedHeight = height;
        var stride = width;

        if (!ComObjects.Failed(type.GetUInt32(in MediaFoundationIds.DefaultStride, out var declared)) && (int)declared > 0)
        {
            stride = (int)declared;
        }

        ReadAperture(type, ref width, ref height);
        format = new Nv12Format(width, height, codedHeight, stride);

        return width > 0 && height > 0;
    }

    // The shown area, when the type states one inside the coded picture (a 1080-line stream coded as 1088).
    private static void ReadAperture(IMFAttributes type, ref int width, ref int height)
    {
        var area = stackalloc byte[ApertureBytes];
        var result = type.GetBlob(in MediaFoundationIds.MinimumDisplayAperture, area, ApertureBytes, out var written);

        if (ComObjects.Failed(result) || written < ApertureBytes)
        {
            return;
        }

        var shownWidth = Unsafe.ReadUnaligned<int>(area + ApertureWidthOffset);
        var shownHeight = Unsafe.ReadUnaligned<int>(area + ApertureHeightOffset);

        if (shownWidth > 0 && shownHeight > 0 && shownWidth <= width && shownHeight <= height)
        {
            width = shownWidth;
            height = shownHeight;
        }
    }
}
