// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>ICodecAPI (strmif.h), up to SetValue: a codec's settings by GUID (codecapi.h's CODECAPI_*).</summary>
[GeneratedComInterface]
[Guid("901db4c7-31ce-41a2-85dc-8fa0bf41b8da")]
internal partial interface ICodecAPI
{
    void IsSupported();

    void IsModifiable();

    void GetParameterRange();

    void GetParameterValues();

    void GetDefaultValue();

    void GetValue();

    [PreserveSig]
    unsafe int SetValue(in Guid api, ComVariant* value);
}
