// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The constant buffer every frame-generation shader reads ("cb"), in the layout D3DReflect reports for it: the field
/// offsets are those of <see cref="LsfgConstantLayout"/>, checked against each shader when the pipeline is built.
/// Forty-eight bytes, the largest buffer (the beta mask step's) rounded to D3D11's sixteen-byte granularity.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LsfgConstants
{
    public uint InputOffsetX;
    public uint InputOffsetY;
    public uint FirstIter;
    public uint FirstIterS;
    public uint AdvancedColorKind;
    public uint HdrSupport;
    public float ResolutionInvScale;
    public float Timestamp;
    public float UiThreshold;
    public uint Padding0;
    public uint Padding1;
    public uint Padding2;
}
