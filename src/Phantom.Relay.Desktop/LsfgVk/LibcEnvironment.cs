// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>The process's native environment through the C library (<c>getenv</c>, <c>setenv</c>, <c>unsetenv</c>).</summary>
[SupportedOSPlatform("linux")]
public sealed class LibcEnvironment : IProcessEnvironment
{
    private const int Overwrite = 1;

    public string? Get(string name)
    {
        return Marshal.PtrToStringUTF8(LibcEnvironmentMethods.GetEnvironment(name));
    }

    public void Set(string name, string value)
    {
        if (LibcEnvironmentMethods.SetEnvironment(name, value, Overwrite) != 0)
        {
            throw new InvalidOperationException($"setenv {name} failed (errno {Marshal.GetLastPInvokeError()})");
        }
    }

    public void Unset(string name)
    {
        if (LibcEnvironmentMethods.UnsetEnvironment(name) != 0)
        {
            throw new InvalidOperationException($"unsetenv {name} failed (errno {Marshal.GetLastPInvokeError()})");
        }
    }
}
