// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Media.Windows.Interop;

/// <summary>D3D11_SAMPLER_DESC (d3d11.h), its border colour flattened into four fields.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SamplerDescription
{
    public uint Filter;
    public uint AddressU;
    public uint AddressV;
    public uint AddressW;
    public float MipLodBias;
    public uint MaxAnisotropy;
    public uint ComparisonFunction;
    public float BorderRed;
    public float BorderGreen;
    public float BorderBlue;
    public float BorderAlpha;
    public float MinLod;
    public float MaxLod;
}
