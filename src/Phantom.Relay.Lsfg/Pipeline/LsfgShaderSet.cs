// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The compute shaders of one set (quality or performance), created on the device from the mapped Lossless.dll and
/// checked against the shapes the pipeline wires: every role's input and output counts, its samplers and its
/// constant layout. A role is asked for by its quality-set id (<see cref="LsfgShaderIds"/>).
/// </summary>
internal sealed class LsfgShaderSet : IDisposable
{
    // The most samplers and constant buffers a frame-generation shader binds (s0, s1 and cb0), as reflected.
    private const int MaxSamplers = 2;
    private const int MaxConstantBuffers = 1;

    private readonly Dictionary<int, (ShaderLayout Layout, nint Shader)> _shaders = [];
    private readonly bool _performance;

    private LsfgShaderSet(bool performance)
    {
        _performance = performance;
    }

    /// <summary>The features alpha keeps per frame and level: its last step's output count (4, or 2 in performance mode).</summary>
    public int Features => Layout(LsfgShaderIds.AlphaFirst + LsfgShaderIds.AlphaSteps - 1).UnorderedAccesses;

    /// <summary>Every role's shader, created and checked; null and the first problem when one is missing or differs.</summary>
    public static LsfgShaderSet? TryCreate(ComputeDevice device, LsfgShaderCatalog catalog, bool performance, out string? problem)
    {
        var set = new LsfgShaderSet(performance);

        foreach (var role in Roles())
        {
            problem = set.TryAdd(device, catalog, role);

            if (problem is not null)
            {
                set.Dispose();

                return null;
            }
        }

        problem = LsfgShapes.Check(set);

        if (problem is not null)
        {
            set.Dispose();

            return null;
        }

        return set;
    }

    /// <summary>A role's reflected layout, by its quality-set id.</summary>
    public ShaderLayout Layout(int role)
    {
        return _shaders[LsfgShaderIds.Of(role, _performance)].Layout;
    }

    /// <summary>A role's compute shader, by its quality-set id.</summary>
    public nint Shader(int role)
    {
        return _shaders[LsfgShaderIds.Of(role, _performance)].Shader;
    }

    /// <summary>The resource id a role resolves to in this set.</summary>
    public int Id(int role)
    {
        return LsfgShaderIds.Of(role, _performance);
    }

    public void Dispose()
    {
        foreach (var entry in _shaders.Values)
        {
            var shader = entry.Shader;
            ComObjects.Release(ref shader);
        }

        _shaders.Clear();
    }

    private static List<int> Roles()
    {
        var roles = new List<int> { LsfgShaderIds.Mipmaps, LsfgShaderIds.Generate, LsfgShaderIds.Correlation, LsfgShaderIds.BlendCorrelation };
        AddRange(roles, LsfgShaderIds.AlphaFirst, LsfgShaderIds.AlphaSteps);
        AddRange(roles, LsfgShaderIds.BetaFirst, LsfgShaderIds.BetaSteps);
        AddRange(roles, LsfgShaderIds.GammaFirst, LsfgShaderIds.GammaSteps);
        AddRange(roles, LsfgShaderIds.DeltaFlowFirst, LsfgShaderIds.DeltaFlowSteps);
        AddRange(roles, LsfgShaderIds.BlendFirst, LsfgShaderIds.BlendSteps);

        return roles;
    }

    private static void AddRange(List<int> roles, int first, int count)
    {
        for (var index = 0; index < count; index++)
        {
            roles.Add(first + index);
        }
    }

    private string? TryAdd(ComputeDevice device, LsfgShaderCatalog catalog, int role)
    {
        var id = LsfgShaderIds.Of(role, _performance);

        if (!catalog.Shaders.TryGetValue(id, out var layout))
        {
            return $"Lossless.dll has no compute shader with id {id}";
        }

        if (layout.Samplers > MaxSamplers || layout.ConstantBuffers > MaxConstantBuffers)
        {
            return $"shader {id} binds {layout.Samplers} samplers and {layout.ConstantBuffers} constant buffers; the pipeline binds {MaxSamplers} and {MaxConstantBuffers}";
        }

        var constants = LsfgConstantLayout.Check(layout);

        if (constants is not null)
        {
            return constants;
        }

        var shader = device.CreateShader(catalog.Bytecode(id));

        if (shader == 0)
        {
            return $"D3D11 refused shader {id} as a compute shader";
        }

        _shaders[id] = (layout, shader);

        return null;
    }
}
