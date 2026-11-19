// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>A console the account can stream from.</summary>
public sealed record XboxConsole(string ServerId, string Name, string ConsoleType, string PowerState, bool IsStreamingEnabled);
