// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// The compute shaders one upscaler runs, created on the device from the mapped Lossless.dll, each checked against
/// its role's shape first (<see cref="UpscaleShape"/>).
/// </summary>
internal sealed class LosslessUpscaleShaders : IDisposable
{
    private readonly Dictionary<int, nint> _shaders = [];

    private LosslessUpscaleShaders()
    {
    }

    /// <summary>The shaders of <paramref name="roles"/> (id and shape); null and the first problem when one is missing or differs.</summary>
    public static LosslessUpscaleShaders? TryCreate(ComputeDevice device, LsfgShaderCatalog catalog, IReadOnlyList<(int Id, UpscaleShape Shape)> roles, out string? problem)
    {
        var set = new LosslessUpscaleShaders();

        foreach (var (id, shape) in roles)
        {
            problem = set.TryAdd(device, catalog, id, shape);

            if (problem is not null)
            {
                set.Dispose();

                return null;
            }
        }

        problem = null;

        return set;
    }

    /// <summary>A role's compute shader, by its resource id.</summary>
    public nint Shader(int id)
    {
        return _shaders[id];
    }

    public void Dispose()
    {
        foreach (var shader in _shaders.Values)
        {
            var owned = shader;
            ComObjects.Release(ref owned);
        }

        _shaders.Clear();
    }

    private string? TryAdd(ComputeDevice device, LsfgShaderCatalog catalog, int id, UpscaleShape shape)
    {
        if (_shaders.ContainsKey(id))
        {
            return null;
        }

        if (!catalog.Shaders.TryGetValue(id, out var layout))
        {
            return $"Lossless.dll has no compute shader with id {id}";
        }

        var problem = shape.Check(layout);

        if (problem is not null)
        {
            return problem;
        }

        var shader = device.CreateShader(catalog.Bytecode(id));

        if (shader == 0)
        {
            return $"D3D11 refused shader {id} as a compute shader";
        }

        _shaders[id] = shader;

        return null;
    }
}
