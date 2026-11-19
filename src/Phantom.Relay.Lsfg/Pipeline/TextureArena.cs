// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// Makes and owns the pipeline's textures. A shader's outputs get the format their reflected component count asks
/// for: one component is a mask or luma level (R8_UNORM), four are features or flow (R16G16B16A16_FLOAT).
/// </summary>
internal sealed class TextureArena : IDisposable
{
    // A typed output with this many components is a single-channel texture.
    private const int SingleChannel = 1;

    private readonly ComputeDevice _device;
    private readonly List<GpuTexture> _textures = [];

    public TextureArena(ComputeDevice device)
    {
        _device = device;
    }

    /// <summary>A texture this arena owns; throws when D3D11 refuses it, which ends the pipeline's build.</summary>
    public GpuTexture Create(int width, int height, uint format, bool writable)
    {
        var texture = _device.CreateTexture(Math.Max(1, width), Math.Max(1, height), format, writable)
            ?? throw new InvalidOperationException($"D3D11 refused a {width}x{height} texture of format {format}");

        _textures.Add(texture);

        return texture;
    }

    /// <summary>
    /// A writable texture per output slot of <paramref name="layout"/>, each of the given size: four-channel features
    /// in R8G8B8A8_UNORM, whose clamp to [0, 1] on store is the networks' activation, or, for a chain's signed
    /// result (a flow, the blend weights), R16G16B16A16_FLOAT.
    /// </summary>
    public GpuTexture[] Outputs(ShaderLayout layout, int width, int height, bool signed = false)
    {
        var outputs = new GpuTexture[layout.UnorderedAccesses];
        var colour = signed ? Direct3D11Values.Rgba16FloatFormat : Direct3D11Values.Rgba8Format;

        for (var slot = 0; slot < outputs.Length; slot++)
        {
            var format = layout.OutputComponents(slot) == SingleChannel ? Direct3D11Values.R8Format : colour;
            outputs[slot] = Create(width, height, format, writable: true);
        }

        return outputs;
    }

    public void Dispose()
    {
        foreach (var texture in _textures)
        {
            texture.Dispose();
        }

        _textures.Clear();
    }
}