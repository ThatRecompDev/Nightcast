// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Shaders;

/// <summary>One resource a shader binds: its name, kind, slot and, for textures, how many components it reads or writes.</summary>
public sealed record ShaderBinding(string Name, ShaderBindingKind Kind, int Slot, int Components);
