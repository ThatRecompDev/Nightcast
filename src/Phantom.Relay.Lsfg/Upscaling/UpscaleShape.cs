// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg.Upscaling;

/// <summary>
/// What an upscaling role's shader must look like as D3DReflect reports it: its thread-group size, the components of
/// each texture it reads and each output it writes (in slot order), how many samplers it binds, and its constant
/// buffer's fields at their offsets. A shader that differs is refused, so a changed Lossless.dll fails loudly.
/// </summary>
internal sealed record UpscaleShape(int Group, int[] Inputs, int[] Outputs, int Samplers)
{
    // The reflected constant fields (D3DReflect, ID3D11ShaderReflectionVariable.GetDesc StartOffset).
    private const int InputConsts1Offset = 0;
    private const int InputConsts2Offset = 16;
    private const int OutputConstsOffset = 32;
    private const int UnknownOffset = -1;

    /// <summary>The problem with <paramref name="layout"/>, or null when it has this shape.</summary>
    public string? Check(ShaderLayout layout)
    {
        if (layout.GroupWidth != Group || layout.GroupHeight != Group)
        {
            return $"shader {layout.Id} runs {layout.GroupWidth}x{layout.GroupHeight} groups; the upscaler expects {Group}x{Group}";
        }

        if (layout.Textures != Inputs.Length || layout.UnorderedAccesses != Outputs.Length || layout.Samplers != Samplers)
        {
            return $"shader {layout.Id} binds {layout.Textures} textures, {layout.UnorderedAccesses} outputs and {layout.Samplers} samplers; the upscaler expects {Inputs.Length}, {Outputs.Length} and {Samplers}";
        }

        return CheckComponents(layout) ?? CheckConstants(layout);
    }

    private string? CheckComponents(ShaderLayout layout)
    {
        for (var slot = 0; slot < Inputs.Length; slot++)
        {
            if (layout.InputComponents(slot) != Inputs[slot])
            {
                return $"shader {layout.Id} reads {layout.InputComponents(slot)} components at t{slot}; the upscaler expects {Inputs[slot]}";
            }
        }

        for (var slot = 0; slot < Outputs.Length; slot++)
        {
            if (layout.OutputComponents(slot) != Outputs[slot])
            {
                return $"shader {layout.Id} writes {layout.OutputComponents(slot)} components at u{slot}; the upscaler expects {Outputs[slot]}";
            }
        }

        return null;
    }

    private static string? CheckConstants(ShaderLayout layout)
    {
        if (layout.ConstantBufferSize > LosslessUpscaleConstants.Size)
        {
            return $"shader {layout.Id} has a {layout.ConstantBufferSize}-byte constant buffer; the upscaler knows {LosslessUpscaleConstants.Size} bytes";
        }

        foreach (var constant in layout.Constants)
        {
            var expected = OffsetOf(constant.Name);

            if (expected != constant.Offset)
            {
                return $"shader {layout.Id} has constant '{constant.Name}' at byte {constant.Offset}; the upscaler expects {expected}";
            }
        }

        return null;
    }

    private static int OffsetOf(string name)
    {
        return name switch
        {
            "InputConsts1" => InputConsts1Offset,
            "InputConsts2" => InputConsts2Offset,
            "OutputConsts" => OutputConstsOffset,
            _ => UnknownOffset,
        };
    }
}
