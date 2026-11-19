// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Audio.Tests.Fakes;

/// <summary>One call to the fake decoder: what it did and the packet index it was for (-1 for a concealment).</summary>
public readonly record struct FakeDecode(FakeDecodeKind Kind, int Index);
