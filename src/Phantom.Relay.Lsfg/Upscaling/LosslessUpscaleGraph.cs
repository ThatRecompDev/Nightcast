// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The passes of one upscaler for one geometry, built once: their textures (LS1's features at the picture's size, its
/// luma detail at twice it, the output), constant buffers and dispatches. The picture's view is not known when they
/// are built; <see cref="PictureSlots"/> says which pass reads it at which slot, filled in per picture.
/// </summary>
internal sealed class LosslessUpscaleGraph : IDisposable
{
    /// <summary>LS1's detail is made at twice the picture's size, whatever the output's.</summary>
    public const int DetailScale = 2;

    // Where the networks read relative to their thread: at the picture's origin, both of them (measured: one texel
    // either way costs about 3.5 dB on the chart; docs/architecture/lossless-upscalers.md).
    private const int NetworkOrigin = 0;

    // LS1's roles in the order Roles lists them: its three steps, then the combine.
    private const int FeaturesStep = 0;
    private const int RefineStep = 1;
    private const int DetailStep = 2;
    private const int CombineStep = 3;

    // The performance setting's roles: its one step, then the combine.
    private const int OneStep = 0;
    private const int OneStepCombine = 1;

    private readonly ComputeDevice _device;
    private readonly TextureArena _arena;
    private readonly List<nint> _buffers = [];
    private readonly List<ComputePass> _passes = [];
    private readonly List<(int Pass, int Slot)> _pictureSlots = [];
    private readonly LosslessUpscaleGeometry _geometry;
    private readonly LosslessUpscaleWiring _wiring;
    private GpuTexture? _output;

    private LosslessUpscaleGraph(ComputeDevice device, LosslessUpscaleGeometry geometry, LosslessUpscaleWiring wiring)
    {
        _device = device;
        _arena = new TextureArena(device);
        _geometry = geometry;
        _wiring = wiring;
    }

    public ComputePass[] Passes { get; private set; } = [];

    /// <summary>The (pass, slot) pairs that read the picture.</summary>
    public (int Pass, int Slot)[] PictureSlots { get; private set; } = [];

    /// <summary>The upscaled picture: an R8G8B8A8 texture of the geometry's output texture size, the output in its top-left corner.</summary>
    public GpuTexture Output => _output!;

    /// <summary>The roles (id and shape) an upscaler with <paramref name="settings"/> runs, in order.</summary>
    public static List<(int Id, UpscaleShape Shape)> Roles(LosslessUpscalerSettings settings)
    {
        var level = settings.Level;

        return settings.Kind switch
        {
            LosslessUpscalerKind.Ls1 =>
            [
                (LosslessUpscaleIds.Ls1(LosslessUpscaleIds.Ls1Features, level), LosslessUpscaleIds.FeaturesShape),
                (LosslessUpscaleIds.Ls1(LosslessUpscaleIds.Ls1Refine, level), LosslessUpscaleIds.RefineShape),
                (LosslessUpscaleIds.Ls1(LosslessUpscaleIds.Ls1Detail, level), LosslessUpscaleIds.DetailShape),
                (LosslessUpscaleIds.Ls1Combine, LosslessUpscaleIds.CombineShape),
            ],
            LosslessUpscalerKind.Ls1Performance =>
            [
                (LosslessUpscaleIds.Ls1PerformanceDetail + level, LosslessUpscaleIds.DetailShape),
                (LosslessUpscaleIds.Ls1Combine, LosslessUpscaleIds.CombineShape),
            ],
            _ => [(LosslessUpscaleIds.Sgsr, LosslessUpscaleIds.SgsrShape)],
        };
    }

    /// <summary>The graph for <paramref name="settings"/> at <paramref name="geometry"/>; null and why when D3D11 refuses a texture.</summary>
    public static LosslessUpscaleGraph? TryBuild(ComputeDevice device, LosslessUpscaleShaders shaders, LosslessUpscalerSettings settings, LosslessUpscaleGeometry geometry, LosslessUpscaleWiring wiring, out string? problem)
    {
        var graph = new LosslessUpscaleGraph(device, geometry, wiring);

        try
        {
            graph.Build(shaders, settings);
            problem = null;

            return graph;
        }
        catch (InvalidOperationException refused)
        {
            graph.Dispose();
            problem = refused.Message;

            return null;
        }
    }

    public void Dispose()
    {
        foreach (var buffer in _buffers)
        {
            var owned = buffer;
            ComObjects.Release(ref owned);
        }

        _buffers.Clear();
        _arena.Dispose();
    }

    private static ReadOnlySpan<byte> Bytes(in LosslessUpscaleConstants values)
    {
        return MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(in values, 1));
    }

    private void Build(LosslessUpscaleShaders shaders, LosslessUpscalerSettings settings)
    {
        _output = _arena.Create(_geometry.OutputTextureWidth, _geometry.OutputTextureHeight, Direct3D11Values.Rgba8Format, writable: true);
        _device.Clear(_output);
        var roles = Roles(settings);

        if (settings.Kind == LosslessUpscalerKind.Ls1)
        {
            BuildThreeStep(shaders, roles);
        }
        else if (settings.Kind == LosslessUpscalerKind.Ls1Performance)
        {
            BuildOneStep(shaders, roles);
        }
        else
        {
            var sgsr = Buffer(Constants(0, (uint)settings.Sharpness));
            AddPicturePass(roles[0].Id, shaders, [_output], sgsr, _geometry.OutputWidth, _geometry.OutputHeight, roles[0].Shape.Group);
        }

        Passes = [.. _passes];
        PictureSlots = [.. _pictureSlots];
    }

    // LS1: features and refine at the picture's size, the detail at twice it, then the combine into the output.
    private void BuildThreeStep(LosslessUpscaleShaders shaders, List<(int Id, UpscaleShape Shape)> roles)
    {
        var (width, height) = (_geometry.InputWidth, _geometry.InputHeight);
        var network = Buffer(Constants(NetworkOrigin + _wiring.NetworkShift, 0));
        var features = _arena.Create(width, height, _wiring.FeatureFormat, writable: true);
        var refined = _arena.Create(width, height, _wiring.FeatureFormat, writable: true);
        var detail = Detail();
        var group = roles[FeaturesStep].Shape.Group;

        AddPicturePass(roles[FeaturesStep].Id, shaders, [features], network, width, height, group);
        _passes.Add(Pass(roles[RefineStep].Id, shaders, [features.View], [refined], network, width, height, group));
        _passes.Add(Pass(roles[DetailStep].Id, shaders, [refined.View], [detail], network, width, height, group));
        AddCombine(shaders, roles[CombineStep].Id, detail);
    }

    // LS1's performance setting: the detail in one step, then the combine.
    private void BuildOneStep(LosslessUpscaleShaders shaders, List<(int Id, UpscaleShape Shape)> roles)
    {
        var network = Buffer(Constants(NetworkOrigin + _wiring.NetworkShift, 0));
        var detail = Detail();

        AddPicturePass(roles[OneStep].Id, shaders, [detail], network, _geometry.InputWidth, _geometry.InputHeight, roles[OneStep].Shape.Group);
        AddCombine(shaders, roles[OneStepCombine].Id, detail);
    }

    // The detail texture, cleared: the networks leave its last two texels of each row and column unwritten.
    private GpuTexture Detail()
    {
        var detail = _arena.Create(_geometry.InputWidth * DetailScale, _geometry.InputHeight * DetailScale, Direct3D11Values.R16FloatFormat, writable: true);
        _device.Clear(detail);

        return detail;
    }

    // The combine reads the detail at t0 and the picture at t1.
    private void AddCombine(LosslessUpscaleShaders shaders, int id, GpuTexture detail)
    {
        var combine = Buffer(Constants(0, 0));
        _pictureSlots.Add((_passes.Count, 1));
        var group = LosslessUpscaleIds.CombineShape.Group;
        _passes.Add(Pass(id, shaders, [detail.View, 0], [_output!], combine, _geometry.OutputWidth, _geometry.OutputHeight, group));
    }

    // A pass whose t0 is the picture.
    private void AddPicturePass(int id, LosslessUpscaleShaders shaders, GpuTexture[] outputs, nint constants, int workWidth, int workHeight, int group)
    {
        _pictureSlots.Add((_passes.Count, 0));
        _passes.Add(Pass(id, shaders, [0], outputs, constants, workWidth, workHeight, group));
    }

    private static ComputePass Pass(int id, LosslessUpscaleShaders shaders, nint[] inputs, GpuTexture[] outputs, nint constants, int workWidth, int workHeight, int group)
    {
        var writes = new nint[outputs.Length];

        for (var index = 0; index < outputs.Length; index++)
        {
            writes[index] = outputs[index].Output;
        }

        var groupsX = Math.Max(1, (workWidth + group - 1) / group);
        var groupsY = Math.Max(1, (workHeight + group - 1) / group);

        return new ComputePass(id, shaders.Shader(id), inputs, writes, constants, groupsX, groupsY);
    }

    private LosslessUpscaleConstants Constants(int origin, uint option)
    {
        return new LosslessUpscaleConstants
        {
            InputWidth = (uint)_geometry.InputWidth,
            InputHeight = (uint)_geometry.InputHeight,
            TextureWidth = (uint)_geometry.InputWidth,
            TextureHeight = (uint)_geometry.InputHeight,
            OriginX = unchecked((uint)origin),
            OriginY = unchecked((uint)origin),
            Option = option,
            OutputWidth = (uint)_geometry.OutputWidth,
            OutputHeight = (uint)_geometry.OutputHeight,
        };
    }

    private nint Buffer(in LosslessUpscaleConstants values)
    {
        var buffer = _device.CreateConstantBuffer(LosslessUpscaleConstants.Size, Bytes(in values));

        if (buffer == 0)
        {
            throw new InvalidOperationException("D3D11 refused the upscaler's constant buffer");
        }

        _buffers.Add(buffer);

        return buffer;
    }
}
