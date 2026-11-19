// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// One dispatch of one Lossless shader, built once: the shader, the views it reads (t0..) and writes (u0..) in slot
/// order, its constant buffer and its thread-group count. Running it allocates nothing.
/// </summary>
internal sealed class ComputePass
{
    public ComputePass(int shaderId, nint shader, nint[] inputs, nint[] outputs, nint constants, int groupsX, int groupsY)
    {
        ShaderId = shaderId;
        Shader = shader;
        Inputs = inputs;
        Outputs = outputs;

        Constants = constants;
        GroupsX = groupsX;
        GroupsY = groupsY;
    }

    /// <summary>The shader's resource id in Lossless.dll, for diagnostics.</summary>
    public int ShaderId { get; }

    public nint Shader { get; }

    public nint[] Inputs { get; }

    public nint[] Outputs { get; }

    public nint Constants { get; }

    public int GroupsX { get; }

    public int GroupsY { get; }
}
