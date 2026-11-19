// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The constant buffer every upscaling shader reads ("cb", 48 bytes), as D3DReflect lays it out: three uint4 fields,
/// InputConsts1 at 0, InputConsts2 at 16, OutputConsts at 32 (checked per shader by <see cref="UpscaleShape"/>). What
/// each component means was read from how the shaders use them (docs/architecture/lossless-upscalers.md).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LosslessUpscaleConstants
{
    /// <summary>The bytes of the buffer.</summary>
    public const int Size = 48;

    // InputConsts1: the picture's width and height, then its texture's (the combine divides by them for its UVs).
    public uint InputWidth;
    public uint InputHeight;
    public uint TextureWidth;
    public uint TextureHeight;

    // InputConsts2: where the picture starts in its texture (the networks add it to the thread's position, wrapping
    // as a signed shift), then a per-shader option (LS1: 1 for an scRGB picture; SGSR: its sharpness), unused.
    public uint OriginX;
    public uint OriginY;
    public uint Option;
    public uint Unused;

    // OutputConsts: the output's width and height, then where it starts in the output texture.
    public uint OutputWidth;
    public uint OutputHeight;
    public uint OutputX;
    public uint OutputY;
}
