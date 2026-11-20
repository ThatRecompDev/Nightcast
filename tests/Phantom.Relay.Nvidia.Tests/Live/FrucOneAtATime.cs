// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Nvidia.Tests.Live;

/// <summary>The FRUC tests run one after another: two FRUC instances alive in one process at once crash it.</summary>
[CollectionDefinition(Name)]
public sealed class FrucOneAtATime
{
    public const string Name = "FRUC, one at a time";
}
