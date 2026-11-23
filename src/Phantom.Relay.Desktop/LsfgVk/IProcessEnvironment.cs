// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// This process's native environment, the one the Vulkan loader and its layers read with <c>getenv</c>. On Unix .NET's
/// <see cref="Environment.SetEnvironmentVariable(string, string)"/> changes only its own copy, so the variables a layer
/// needs are set through the C library (<see cref="LibcEnvironment"/>).
/// </summary>
public interface IProcessEnvironment
{
    string? Get(string name);

    void Set(string name, string value);

    void Unset(string name);
}
