// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Lsfg.Interop;

/// <summary>D3D11_SHADER_DESC (d3d11shader.h), every field in the header's order; only a few are read.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ShaderDescription
{
    public uint Version;
    public nint Creator;
    public uint Flags;
    public uint ConstantBuffers;
    public uint BoundResources;
    public uint InputParameters;
    public uint OutputParameters;
    public uint InstructionCount;
    public uint TempRegisterCount;
    public uint TempArrayCount;
    public uint DefCount;
    public uint DclCount;
    public uint TextureNormalInstructions;
    public uint TextureLoadInstructions;
    public uint TextureCompInstructions;
    public uint TextureBiasInstructions;
    public uint TextureGradientInstructions;
    public uint FloatInstructionCount;
    public uint IntInstructionCount;
    public uint UintInstructionCount;
    public uint StaticFlowControlCount;
    public uint DynamicFlowControlCount;
    public uint MacroInstructionCount;
    public uint ArrayInstructionCount;
    public uint CutInstructionCount;
    public uint EmitInstructionCount;
    public uint GeometryOutputTopology;
    public uint GeometryMaxOutputVertexCount;
    public uint InputPrimitive;
    public uint PatchConstantParameters;
    public uint GeometryInstanceCount;
    public uint ControlPoints;
    public uint HullOutputPrimitive;
    public uint HullPartitioning;
    public uint TessellatorDomain;
    public uint BarrierInstructions;
    public uint InterlockedInstructions;
    public uint TextureStoreInstructions;
}
