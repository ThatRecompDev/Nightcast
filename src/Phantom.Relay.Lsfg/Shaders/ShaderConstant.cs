// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>One field of a shader's constant buffer: its name, byte offset and size, and whether the shader reads it.</summary>
public sealed record ShaderConstant(string Name, int Offset, int Size, bool Used);
