// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>
/// What D3DReflect reports about one compute shader of Lossless.dll: its resource id, thread-group size, bindings
/// and constant-buffer layout. Nothing of the shader's code is kept.
/// </summary>
public sealed class ShaderLayout
{
    public ShaderLayout(int id, int groupWidth, int groupHeight, IReadOnlyList<ShaderBinding> bindings, int constantBufferSize, IReadOnlyList<ShaderConstant> constants)
    {
        Id = id;
        GroupWidth = groupWidth;
        GroupHeight = groupHeight;
        Bindings = bindings;
        ConstantBufferSize = constantBufferSize;
        Constants = constants;
    }

    /// <summary>The RT_RCDATA resource id in Lossless.dll.</summary>
    public int Id { get; }

    public int GroupWidth { get; }

    public int GroupHeight { get; }

    public IReadOnlyList<ShaderBinding> Bindings { get; }

    /// <summary>The size of constant buffer 0 in bytes, or 0 when the shader has none.</summary>
    public int ConstantBufferSize { get; }

    public IReadOnlyList<ShaderConstant> Constants { get; }

    public int Textures => Count(ShaderBindingKind.Texture);

    public int UnorderedAccesses => Count(ShaderBindingKind.UnorderedAccess);

    public int Samplers => Count(ShaderBindingKind.Sampler);

    public int ConstantBuffers => Count(ShaderBindingKind.ConstantBuffer);

    /// <summary>The components a typed output writes (1 for a single-channel mask, 4 for a colour or feature texture).</summary>
    public int OutputComponents(int slot)
    {
        return Components(ShaderBindingKind.UnorderedAccess, slot);
    }

    /// <summary>The components a texture input reads.</summary>
    public int InputComponents(int slot)
    {
        return Components(ShaderBindingKind.Texture, slot);
    }

    /// <summary>A constant by name, or null when the buffer has no such field.</summary>
    public ShaderConstant? Constant(string name)
    {
        foreach (var constant in Constants)
        {
            if (constant.Name == name)
            {
                return constant;
            }
        }

        return null;
    }

    private int Count(ShaderBindingKind kind)
    {
        var count = 0;

        foreach (var binding in Bindings)
        {
            if (binding.Kind == kind)
            {
                count++;
            }
        }

        return count;
    }

    private int Components(ShaderBindingKind kind, int slot)
    {
        foreach (var binding in Bindings)
        {
            if (binding.Kind == kind && binding.Slot == slot)
            {
                return binding.Components;
            }
        }

        return 0;
    }
}
