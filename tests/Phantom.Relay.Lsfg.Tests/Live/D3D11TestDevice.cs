// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>A hardware D3D11 device with video support, as the decoder makes one, and texture upload and read-back.</summary>
internal sealed unsafe class D3D11TestDevice : IDisposable
{
    private const uint BgraSupport = 0x20;

    private readonly ComWrappers _wrappers = new StrategyBasedComWrappers();
    private readonly ID3D11Device _deviceApi;
    private readonly ID3D11DeviceContext _contextApi;
    private readonly List<nint> _textures = [];
    private nint _device;
    private nint _context;

    public D3D11TestDevice()
    {
        var result = NativeMethods.D3D11CreateDevice(0, MediaFoundationValues.HardwareDriver, 0, MediaFoundationValues.VideoSupport | BgraSupport, 0, 0, MediaFoundationValues.D3D11SdkVersion, out _device, out _, out _context);
        Assert.True(result >= 0, $"D3D11CreateDevice failed (0x{result:X8})");
        _deviceApi = ComObjects.Wrap<ID3D11Device>(_wrappers, _device);
        _contextApi = ComObjects.Wrap<ID3D11DeviceContext>(_wrappers, _context);
    }

    public nint Device => _device;

    /// <summary>An R8G8B8A8 texture holding <paramref name="pixels"/>, readable by shaders.</summary>
    public nint CreateRgba(int width, int height, byte[] pixels)
    {
        return Create(width, height, Direct3D11Values.Rgba8Format, Direct3D11Values.BindShaderResource, pixels, width * 4);
    }

    /// <summary>
    /// An NV12 texture bound as a decoder's surface is: the luma plane then the interleaved chroma plane, rows
    /// <paramref name="width"/> bytes apart. (A video processor refuses an NV12 texture bound for shader reads only.)
    /// </summary>
    public nint CreateNv12(int width, int height, byte[] planes)
    {
        return Create(width, height, MediaFoundationValues.Nv12Format, Direct3D11Values.BindDecoder | Direct3D11Values.BindShaderResource, planes, width);
    }

    /// <summary>An NV12 texture a video processor writes (render target) and a renderer reads, as a slot of the decoder's shared ring.</summary>
    public nint CreateNv12Target(int width, int height)
    {
        var description = new Texture2DDescription { Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = MediaFoundationValues.Nv12Format, SampleCount = 1, BindFlags = Direct3D11Values.BindRenderTarget | Direct3D11Values.BindShaderResource };
        var result = _deviceApi.CreateTexture2D(in description, 0, out var texture);
        Assert.True(result >= 0, $"CreateTexture2D failed (0x{result:X8})");
        _textures.Add(texture);

        return texture;
    }

    /// <summary>Copies one texture into another of the same size and format, on the GPU.</summary>
    public void Copy(nint destination, nint source)
    {
        _contextApi.CopyResource(destination, source);
    }

    /// <summary>An R8G8B8A8 texture's pixels, through a staging copy.</summary>
    public byte[] ReadRgba(nint texture, int width, int height)
    {
        var description = new Texture2DDescription { Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = Direct3D11Values.Rgba8Format, SampleCount = 1, Usage = MediaFoundationValues.StagingUsage, CpuAccessFlags = MediaFoundationValues.CpuRead };
        Assert.True(_deviceApi.CreateTexture2D(in description, 0, out var staging) >= 0);
        _contextApi.CopyResource(staging, texture);
        Assert.True(_contextApi.Map(staging, 0, MediaFoundationValues.MapRead, 0, out var mapped) >= 0);
        var pixels = new byte[width * height * 4];

        for (var row = 0; row < height; row++)
        {
            new ReadOnlySpan<byte>((byte*)mapped.Data + (row * mapped.RowPitch), width * 4).CopyTo(pixels.AsSpan(row * width * 4));
        }

        _contextApi.Unmap(staging, 0);
        ComObjects.Release(ref staging);

        return pixels;
    }

    /// <summary>An NV12 texture's luma plane, through a staging copy.</summary>
    public byte[] ReadLuma(nint texture, int width, int height)
    {
        var description = new Texture2DDescription { Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = MediaFoundationValues.Nv12Format, SampleCount = 1, Usage = MediaFoundationValues.StagingUsage, CpuAccessFlags = MediaFoundationValues.CpuRead };
        Assert.True(_deviceApi.CreateTexture2D(in description, 0, out var staging) >= 0);
        _contextApi.CopyResource(staging, texture);
        Assert.True(_contextApi.Map(staging, 0, MediaFoundationValues.MapRead, 0, out var mapped) >= 0);
        var luma = new byte[width * height];

        for (var row = 0; row < height; row++)
        {
            new ReadOnlySpan<byte>((byte*)mapped.Data + (row * mapped.RowPitch), width).CopyTo(luma.AsSpan(row * width));
        }

        _contextApi.Unmap(staging, 0);
        ComObjects.Release(ref staging);

        return luma;
    }

    public void Dispose()
    {
        for (var index = 0; index < _textures.Count; index++)
        {
            var texture = _textures[index];
            ComObjects.Release(ref texture);
        }

        ComObjects.Free(_deviceApi);
        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _device);
    }

    private nint Create(int width, int height, uint format, uint bind, byte[] data, int pitch)
    {
        var description = new Texture2DDescription { Width = (uint)width, Height = (uint)height, MipLevels = 1, ArraySize = 1, Format = format, SampleCount = 1, BindFlags = bind };

        fixed (byte* bytes = data)
        {
            var initial = new SubresourceData { Memory = (nint)bytes, RowPitch = (uint)pitch };
            var result = _deviceApi.CreateTexture2D(in description, (nint)(&initial), out var texture);
            Assert.True(result >= 0, $"CreateTexture2D failed (0x{result:X8})");
            _textures.Add(texture);

            return texture;
        }
    }
}
