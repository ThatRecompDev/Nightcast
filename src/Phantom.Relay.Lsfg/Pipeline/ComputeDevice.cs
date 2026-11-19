// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Engine.Core;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// A D3D11 device (the hardware decoder's) and its immediate context, seen as a compute machine: textures with
/// their views, compute shaders from bytecode, constant buffers, samplers, and the dispatch of one pass with its
/// bindings. It holds a reference of its own to the device and context.
/// </summary>
internal sealed unsafe class ComputeDevice : IDisposable
{
    // The most textures or outputs one pass binds (beta's first step reads twelve), and the two samplers (s0, s1).
    private const int MaxViews = 16;
    private const int MaxSamplers = 2;

    // ClearUnorderedAccessViewFloat's four values, one per component (stackalloc is zeroed).
    private const int ClearComponents = 4;

    private readonly ID3D11Device _deviceApi;
    private readonly ID3D11DeviceContext _contextApi;
    private nint _device;
    private nint _context;

    private ComputeDevice(ComWrappers wrappers, nint device, nint context)
    {
        Wrappers = wrappers;
        _device = device;
        _context = context;
        _deviceApi = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        _contextApi = ComObjects.Wrap<ID3D11DeviceContext>(wrappers, context);
    }

    public ComWrappers Wrappers { get; }

    public nint Device => _device;

    public nint Context => _context;

    public ID3D11Device DeviceApi => _deviceApi;

    public ID3D11DeviceContext ContextApi => _contextApi;

    /// <summary>The device behind <paramref name="device"/> (an ID3D11Device pointer the caller keeps owning).</summary>
    public static ComputeDevice Open(ComWrappers wrappers, nint device)
    {
        _ = Marshal.AddRef(device);
        var api = ComObjects.Wrap<ID3D11Device>(wrappers, device);
        api.GetImmediateContext(out var context);
        ComObjects.Free(api);

        return new ComputeDevice(wrappers, device, context);
    }

    /// <summary>A 2D texture with a shader view and, when asked, an unordered-access view; null when D3D11 refuses.</summary>
    public GpuTexture? CreateTexture(int width, int height, uint format, bool writable)
    {
        var bind = Direct3D11Values.BindShaderResource;

        if (writable)
        {
            bind |= Direct3D11Values.BindUnorderedAccess;
        }

        var description = new Texture2DDescription
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = format,
            SampleCount = 1,
            BindFlags = bind,
        };

        if (ComObjects.Failed(_deviceApi.CreateTexture2D(in description, 0, out var texture)))
        {
            return null;
        }

        return GpuTexture.Wrap(this, texture, width, height, format, writable);
    }

    /// <summary>A shader view of a 2D texture the caller owns, read as <paramref name="format"/>; 0 when D3D11 refuses it.</summary>
    public nint CreateView(nint texture, uint format)
    {
        var description = new ShaderResourceViewDescription
        {
            Format = format,
            ViewDimension = Direct3D11Values.ShaderResourceTexture2D,
            MipLevels = 1,
        };

        _ = _deviceApi.CreateShaderResourceView(texture, in description, out var view);

        return view;
    }

    /// <summary>Sets every texel of a writable texture to zero, on the GPU.</summary>
    public void Clear(GpuTexture texture)
    {
        var zero = stackalloc float[ClearComponents];
        _contextApi.ClearUnorderedAccessViewFloat(texture.Output, zero);
    }

    /// <summary>A compute shader made from compiled bytecode (never compiled here); 0 when D3D11 refuses it.</summary>
    public nint CreateShader(ReadOnlySpan<byte> bytecode)
    {
        nint shader;

        fixed (byte* data = bytecode)
        {
            _ = _deviceApi.CreateComputeShader(data, (nuint)bytecode.Length, 0, out shader);
        }

        return shader;
    }

    /// <summary>A constant buffer of <paramref name="size"/> bytes (a multiple of 16) holding <paramref name="contents"/>.</summary>
    public nint CreateConstantBuffer(int size, ReadOnlySpan<byte> contents)
    {
        var description = new BufferDescription
        {
            ByteWidth = (uint)size,
            Usage = Direct3D11Values.DefaultUsage,
            BindFlags = Direct3D11Values.BindConstantBuffer,
        };

        fixed (byte* data = contents)
        {
            var initial = new SubresourceData { Memory = (nint)data };
            _ = _deviceApi.CreateBuffer(in description, &initial, out var buffer);

            return buffer;
        }
    }

    /// <summary>A bilinear sampler that clamps at the edges.</summary>
    public nint CreateLinearClampSampler()
    {
        return CreateClampSampler(Direct3D11Values.LinearFilter);
    }

    /// <summary>A sampler with <paramref name="filter"/> (a D3D11_FILTER) that clamps at the edges.</summary>
    public nint CreateClampSampler(uint filter)
    {
        var description = new SamplerDescription
        {
            Filter = filter,
            AddressU = Direct3D11Values.ClampAddress,
            AddressV = Direct3D11Values.ClampAddress,
            AddressW = Direct3D11Values.ClampAddress,
            MaxAnisotropy = 1,
            ComparisonFunction = Direct3D11Values.ComparisonNever,
            MaxLod = Direct3D11Values.MaxLod,
        };

        _ = _deviceApi.CreateSamplerState(in description, out var sampler);

        return sampler;
    }

    /// <summary>Rewrites a constant buffer's whole contents.</summary>
    [HotPath]
    public void Update(nint buffer, ReadOnlySpan<byte> contents)
    {
        fixed (byte* data = contents)
        {
            _contextApi.UpdateSubresource(buffer, 0, 0, data, 0, 0);
        }
    }

    /// <summary>Copies one texture into another of the same size and format, on the GPU.</summary>
    [HotPath]
    public void Copy(nint destination, nint source)
    {
        _contextApi.CopyResource(destination, source);
    }

    /// <summary>Runs one compute pass: binds its textures, outputs, samplers and constants, dispatches, then unbinds.</summary>
    [HotPath]
    public void Run(ComputePass pass, nint sampler)
    {
        var states = stackalloc nint[MaxSamplers];
        var constants = pass.Constants;
        states[0] = sampler;
        states[1] = sampler;

        fixed (nint* inputs = pass.Inputs)
        {
            fixed (nint* outputs = pass.Outputs)
            {
                _contextApi.CSSetShader(pass.Shader, 0, 0);
                _contextApi.CSSetShaderResources(0, (uint)pass.Inputs.Length, inputs);
                _contextApi.CSSetUnorderedAccessViews(0, (uint)pass.Outputs.Length, outputs, null);
                _contextApi.CSSetSamplers(0, MaxSamplers, states);
                _contextApi.CSSetConstantBuffers(0, 1, &constants);
                _contextApi.Dispatch((uint)pass.GroupsX, (uint)pass.GroupsY, 1);
            }
        }

        // Unbind (stackalloc is zeroed), so the next pass may read what this one wrote without D3D11 unbinding it.
        var empty = stackalloc nint[MaxViews];
        _contextApi.CSSetShaderResources(0, (uint)pass.Inputs.Length, empty);
        _contextApi.CSSetUnorderedAccessViews(0, (uint)pass.Outputs.Length, empty, null);
    }

    public void Dispose()
    {
        ComObjects.Free(_deviceApi);
        ComObjects.Free(_contextApi);
        ComObjects.Release(ref _context);
        ComObjects.Release(ref _device);
    }
}
