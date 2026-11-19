// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>What a shader binds at a slot, as D3DReflect reports it (D3D_SHADER_INPUT_TYPE).</summary>
public enum ShaderBindingKind
{
    /// <summary>A kind the frame generation does not use.</summary>
    Other,

    /// <summary>A constant buffer (cb#).</summary>
    ConstantBuffer,

    /// <summary>A texture read (t#).</summary>
    Texture,

    /// <summary>A sampler (s#).</summary>
    Sampler,

    /// <summary>A typed texture written (u#).</summary>
    UnorderedAccess,
}
