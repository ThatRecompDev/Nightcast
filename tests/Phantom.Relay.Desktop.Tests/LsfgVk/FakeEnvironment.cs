// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>A process environment in a dictionary.</summary>
internal sealed class FakeEnvironment : IProcessEnvironment
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public string? Get(string name)
    {
        return Values.GetValueOrDefault(name);
    }

    public void Set(string name, string value)
    {
        Values[name] = value;
    }

    public void Unset(string name)
    {
        Values.Remove(name);
    }
}
