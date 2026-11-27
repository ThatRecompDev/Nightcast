// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Channels;

/// <summary>What <see cref="PadAnnouncement.Next"/> asks the control channel to send.</summary>
public enum PadStep
{
    None,

    /// <summary>The pad is added.</summary>
    Announce,

    /// <summary>The pad is removed and added again: the console did not answer the last announcement.</summary>
    Reannounce,
}
