// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

/// <summary>A layer check that answers from a list, the last answer repeated, and keeps the environment each check saw.</summary>
internal sealed class FakeProbe : ILayerProbe
{
    private readonly Queue<LayerCheck> _answers;
    private readonly FakeEnvironment _environment;

    public FakeProbe(FakeEnvironment environment, params LayerCheck[] answers)
    {
        _environment = environment;
        _answers = new Queue<LayerCheck>(answers);
    }

    public List<Dictionary<string, string>> Seen { get; } = [];

    public LayerCheck Check()
    {
        Seen.Add(new Dictionary<string, string>(_environment.Values, StringComparer.Ordinal));

        if (_answers.Count > 1)
        {
            return _answers.Dequeue();
        }

        return _answers.Peek();
    }
}
