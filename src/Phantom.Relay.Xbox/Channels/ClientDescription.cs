// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Channels;

/// <summary>
/// What the client tells the console about itself after the handshake: the stream's shown size, its installation id
/// and one fresh message id per description message (<see cref="MessageCount"/>, drawn by the caller from its random
/// source).
/// </summary>
public sealed record ClientDescription(int Width, int Height, string InstallId, IReadOnlyList<string> Ids)
{
    public const int MessageCount = 6;
}
