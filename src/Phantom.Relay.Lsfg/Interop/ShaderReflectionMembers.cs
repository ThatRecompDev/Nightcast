// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>
/// ID3D11ShaderReflectionConstantBuffer and ID3D11ShaderReflectionVariable (d3d11shader.h): plain vtables without
/// IUnknown, owned by their reflection object, so they are called through function pointers at their header slots
/// rather than wrapped as COM objects.
/// </summary>
internal static unsafe class ShaderReflectionMembers
{
    // ID3D11ShaderReflectionConstantBuffer: GetDesc, GetVariableByIndex, GetVariableByName.
    private const int BufferGetDescSlot = 0;
    private const int BufferGetVariableSlot = 1;

    // ID3D11ShaderReflectionVariable: GetDesc, GetType, GetBuffer, GetInterfaceSlot.
    private const int VariableGetDescSlot = 0;

    /// <summary>A constant buffer's name, size and variable count.</summary>
    public static int GetBufferDescription(nint buffer, out ShaderBufferDescription description)
    {
        var method = (delegate* unmanaged<nint, ShaderBufferDescription*, int>)Slot(buffer, BufferGetDescSlot);

        fixed (ShaderBufferDescription* target = &description)
        {
            return method(buffer, target);
        }
    }

    /// <summary>A constant buffer's variable by index; never null (an out-of-range index gives a dummy).</summary>
    public static nint GetVariable(nint buffer, uint index)
    {
        var method = (delegate* unmanaged<nint, uint, nint>)Slot(buffer, BufferGetVariableSlot);

        return method(buffer, index);
    }

    /// <summary>A variable's name, offset, size and whether the shader reads it.</summary>
    public static int GetVariableDescription(nint variable, out ShaderVariableDescription description)
    {
        var method = (delegate* unmanaged<nint, ShaderVariableDescription*, int>)Slot(variable, VariableGetDescSlot);

        fixed (ShaderVariableDescription* target = &description)
        {
            return method(variable, target);
        }
    }

    private static nint Slot(nint instance, int slot)
    {
        var table = *(nint**)instance;

        return table[slot];
    }
}
