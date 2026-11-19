// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Media.Windows;

/// <summary>Making a Media Foundation transform by its CLSID and setting it up through its attributes and ICodecAPI.</summary>
internal static unsafe class MediaFoundationTransforms
{
    /// <summary>Media Foundation, started for the caller (each start is paired with an MFShutdown).</summary>
    public static int Start()
    {
        // The decoder's thread joins the process's multithreaded apartment; a thread already in another keeps it.
        _ = NativeMethods.CoInitializeEx(0, MediaFoundationValues.MultithreadedApartment);

        return NativeMethods.MFStartup(MediaFoundationValues.Version, MediaFoundationValues.StartupFull);
    }

    /// <summary>The transform of <paramref name="classId"/>; the caller releases the pointer and frees the wrapper.</summary>
    public static int TryCreate(ComWrappers wrappers, in Guid classId, out nint transform, out IMFTransform? api)
    {
        api = null;
        var result = NativeMethods.CoCreateInstance(in classId, 0, MediaFoundationValues.InProcessServer, in MediaFoundationIds.TransformInterface, out transform);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        api = ComObjects.Wrap<IMFTransform>(wrappers, transform);

        return result;
    }

    /// <summary>Sets a 32-bit value in the transform's own attributes (MF_LOW_LATENCY).</summary>
    public static int SetAttribute(ComWrappers wrappers, IMFTransform api, in Guid key, uint value)
    {
        var result = api.GetAttributes(out var attributes);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        var store = ComObjects.Wrap<IMFAttributes>(wrappers, attributes);
        result = store.SetUInt32(in key, value);
        ComObjects.Free(store);
        ComObjects.Release(ref attributes);

        return result;
    }

    /// <summary>A 32-bit value of the transform's attributes; false when it has none.</summary>
    public static bool TryGetAttribute(ComWrappers wrappers, IMFTransform api, in Guid key, out uint value)
    {
        value = 0;

        if (ComObjects.Failed(api.GetAttributes(out var attributes)))
        {
            return false;
        }

        var store = ComObjects.Wrap<IMFAttributes>(wrappers, attributes);
        var result = store.GetUInt32(in key, out value);
        ComObjects.Free(store);
        ComObjects.Release(ref attributes);

        return !ComObjects.Failed(result);
    }

    /// <summary>A codec setting through ICodecAPI (CODECAPI_*); the transform's HRESULT, or its QueryInterface's.</summary>
    public static int SetCodecValue(ComWrappers wrappers, nint transform, in Guid setting, ComVariant value)
    {
        var result = Marshal.QueryInterface(transform, in MediaFoundationIds.CodecApiInterface, out var codec);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        var api = ComObjects.Wrap<ICodecAPI>(wrappers, codec);
        result = api.SetValue(in setting, &value);
        ComObjects.Free(api);
        ComObjects.Release(ref codec);

        return result;
    }

    /// <summary>
    /// Low-latency mode, both ways Windows' H.264 decoder reads it: MF_LOW_LATENCY in its attributes and
    /// CODECAPI_AVLowLatencyMode through ICodecAPI (the same GUID). It then emits each picture as soon as it is decoded.
    /// </summary>
    public static int EnableLowLatency(ComWrappers wrappers, nint transform, IMFTransform api)
    {
        var result = SetAttribute(wrappers, api, in MediaFoundationIds.LowLatency, 1);

        if (ComObjects.Failed(result))
        {
            return result;
        }

        return SetCodecValue(wrappers, transform, in MediaFoundationIds.LowLatency, ComVariant.Create(1u));
    }
}
