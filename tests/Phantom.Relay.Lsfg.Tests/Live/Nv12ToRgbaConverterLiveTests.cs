// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>The video processor's NV12 to RGBA on this machine's GPU, on a picture of known limited-range values.</summary>
[Trait("Category", "Live")]
public sealed class Nv12ToRgbaConverterLiveTests
{
    private const int Width = 2560;
    private const int Height = 1440;

    [Fact]
    public void Limited_range_grey_levels_come_out_as_full_range_rgb()
    {
        using var device = new D3D11TestDevice(0);
        var planes = new byte[Width * Height * 3 / 2];

        // Left half luma 16 (black), right half 235 (white); chroma neutral.
        for (var y = 0; y < Height; y++)
        {
            planes.AsSpan(y * Width, Width / 2).Fill(16);
            planes.AsSpan((y * Width) + (Width / 2), Width / 2).Fill(235);
        }

        planes.AsSpan(Width * Height).Fill(128);
        var nv12 = device.CreateNv12(Width, Height, planes);
        using var converter = Nv12ToRgbaConverter.TryCreate(device.Device, Width, Height, out var problem);
        Assert.True(converter is not null, problem);

        Assert.True(converter.Convert(nv12, 0));
        var pixels = device.ReadRgba(converter.Output, Width, Height);

        var black = ((Height / 2 * Width) + (Width / 4)) * 4;
        var white = ((Height / 2 * Width) + (Width * 3 / 4)) * 4;
        Assert.InRange(pixels[black], 0, 4);
        Assert.InRange(pixels[white], 251, 255);
        Assert.InRange(pixels[white + 2], 251, 255);
    }
}
