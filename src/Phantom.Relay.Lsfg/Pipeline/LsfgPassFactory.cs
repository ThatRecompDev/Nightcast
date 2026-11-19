// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// Makes the passes of the graph: a role's shader with its inputs' shader views in slot order (t0..), its outputs'
/// unordered-access views (u0..), a constant buffer, and enough thread groups (the reflected group size) to cover
/// the work, one thread per output pixel unless a shader says otherwise.
/// </summary>
internal sealed class LsfgPassFactory
{
    // Each pyramid level halves the one before, rounding up.
    private const int Halving = 2;

    private readonly LsfgShaderSet _set;

    public LsfgPassFactory(LsfgShaderSet set)
    {
        _set = set;
    }

    /// <summary>Half a size, rounded up, at least one.</summary>
    public static int Half(int size)
    {
        return Math.Max(1, (size + 1) / Halving);
    }

    /// <summary>The shader views of the given textures, in order.</summary>
    public static nint[] Views(params GpuTexture[][] groups)
    {
        var views = new List<nint>();

        foreach (var group in groups)
        {
            foreach (var texture in group)
            {
                views.Add(texture.View);
            }
        }

        return [.. views];
    }

    /// <summary>A pass over <paramref name="workWidth"/> x <paramref name="workHeight"/> threads.</summary>
    public ComputePass Pass(int role, nint[] inputs, GpuTexture[] outputs, nint constants, int workWidth, int workHeight)
    {
        var layout = _set.Layout(role);
        var groupsX = Groups(workWidth, layout.GroupWidth);
        var groupsY = Groups(workHeight, layout.GroupHeight);

        return new ComputePass(_set.Id(role), _set.Shader(role), inputs, Writes(outputs), constants, groupsX, groupsY);
    }

    /// <summary>A pass with one thread per pixel of its first output.</summary>
    public ComputePass Pass(int role, nint[] inputs, GpuTexture[] outputs, nint constants)
    {
        return Pass(role, inputs, outputs, constants, outputs[0].Width, outputs[0].Height);
    }

    private static int Groups(int work, int group)
    {
        return Math.Max(1, (work + group - 1) / group);
    }

    private static nint[] Writes(GpuTexture[] outputs)
    {
        var views = new nint[outputs.Length];

        for (var index = 0; index < outputs.Length; index++)
        {
            views[index] = outputs[index].Output;
        }

        return views;
    }
}
