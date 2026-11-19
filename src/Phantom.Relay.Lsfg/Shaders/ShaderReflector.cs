// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Lsfg.Interop;
using Phantom.Relay.Media.Windows.Interop;

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>
/// Reads what Windows reports about a compiled shader (D3DReflect): the program type, thread-group size, bindings
/// and the fields of constant buffer 0. Only these facts are kept; the blob itself is never copied or logged.
/// </summary>
internal static unsafe class ShaderReflector
{
    /// <summary>Whether a blob starts with the DXBC container's magic, so it is a compiled shader worth reflecting.</summary>
    public static bool IsShaderContainer(ReadOnlySpan<byte> blob)
    {
        return blob.Length >= sizeof(uint) && MemoryMarshal.Read<uint>(blob) == ShaderReflectionValues.ContainerMagic;
    }

    /// <summary>The layout of a compute shader; null and why when the blob is not one.</summary>
    public static ShaderLayout? TryReflect(ComWrappers wrappers, int id, ReadOnlySpan<byte> blob, out string? problem)
    {
        nint reflector;
        int result;

        fixed (byte* data = blob)
        {
            result = LosslessNativeMethods.D3DReflect(data, (nuint)blob.Length, in ShaderReflectionValues.ReflectionInterface, out reflector);
        }

        if (ComObjects.Failed(result))
        {
            problem = $"D3DReflect refused resource {id} (0x{result:X8})";

            return null;
        }

        var api = ComObjects.Wrap<ID3D11ShaderReflection>(wrappers, reflector);

        try
        {
            return Read(api, id, out problem);
        }
        finally
        {
            ComObjects.Free(api);
            ComObjects.Release(ref reflector);
        }
    }

    private static ShaderLayout? Read(ID3D11ShaderReflection api, int id, out string? problem)
    {
        ShaderDescription description;
        var result = api.GetDesc(&description);

        if (ComObjects.Failed(result) || description.Version >> ShaderReflectionValues.ProgramTypeShift != ShaderReflectionValues.ComputeProgram)
        {
            problem = $"resource {id} is not a compute shader";

            return null;
        }

        uint width;
        uint height;
        uint depth;
        _ = api.GetThreadGroupSize(&width, &height, &depth);

        var bindings = ReadBindings(api, description.BoundResources);
        var constants = new List<ShaderConstant>();
        var size = description.ConstantBuffers > 0 ? ReadConstants(api.GetConstantBufferByIndex(0), constants) : 0;
        problem = null;

        return new ShaderLayout(id, (int)width, (int)height, bindings, size, constants);
    }

    private static List<ShaderBinding> ReadBindings(ID3D11ShaderReflection api, uint count)
    {
        var bindings = new List<ShaderBinding>((int)count);

        for (var index = 0u; index < count; index++)
        {
            ShaderInputBindDescription binding;

            if (ComObjects.Failed(api.GetResourceBindingDesc(index, &binding)))
            {
                continue;
            }

            var name = Marshal.PtrToStringAnsi(binding.Name) ?? string.Empty;
            var components = (int)((binding.Flags & ShaderReflectionValues.ComponentMask) >> ShaderReflectionValues.ComponentShift) + 1;
            bindings.Add(new ShaderBinding(name, KindOf(binding.Type), (int)binding.BindPoint, components));
        }

        return bindings;
    }

    private static int ReadConstants(nint buffer, List<ShaderConstant> constants)
    {
        if (ComObjects.Failed(ShaderReflectionMembers.GetBufferDescription(buffer, out var description)))
        {
            return 0;
        }

        for (var index = 0u; index < description.Variables; index++)
        {
            var variable = ShaderReflectionMembers.GetVariable(buffer, index);

            if (ComObjects.Failed(ShaderReflectionMembers.GetVariableDescription(variable, out var field)))
            {
                continue;
            }

            var name = Marshal.PtrToStringAnsi(field.Name) ?? string.Empty;
            var used = (field.Flags & ShaderReflectionValues.VariableUsed) != 0;
            constants.Add(new ShaderConstant(name, (int)field.StartOffset, (int)field.Size, used));
        }

        return (int)description.Size;
    }

    private static ShaderBindingKind KindOf(uint type)
    {
        return type switch
        {
            ShaderReflectionValues.ConstantBufferInput => ShaderBindingKind.ConstantBuffer,
            ShaderReflectionValues.TextureInput => ShaderBindingKind.Texture,
            ShaderReflectionValues.SamplerInput => ShaderBindingKind.Sampler,
            ShaderReflectionValues.TypedUavInput => ShaderBindingKind.UnorderedAccess,
            _ => ShaderBindingKind.Other,
        };
    }
}
