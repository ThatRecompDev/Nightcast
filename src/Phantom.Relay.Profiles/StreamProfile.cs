// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles.KeyboardMouse;

namespace Phantom.Relay.Profiles;

/// <summary>
/// The settings for one game (or the default, with no title ids): the stream tier, frame generation, upscaling, the
/// pad and keyboard and mouse played as the pad. Kept as JSON in the user's store, one value per profile, so players
/// can share them.
/// </summary>
public sealed record StreamProfile(
    string Name,
    IReadOnlyList<string> TitleIds,
    StreamTier Stream,
    FrameGeneration FrameGeneration,
    Upscaling Upscaling,
    PadSettings Pad,
    bool ShowStats,
    KeyboardMouseSettings KeyboardMouse)
{
    public const string DefaultName = "Default";

    public bool IsDefault => TitleIds.Count == 0;

    /// <summary>
    /// The profile used when nothing else matches: 1440p, no frame generation, the sharp upscaler, keyboard and mouse
    /// off.
    /// </summary>
    public static StreamProfile Default { get; } = new(
        DefaultName,
        [],
        StreamTier.Standard,
        FrameGeneration.Off,
        Upscaling.Standard,
        PadSettings.Standard,
        ShowStats: false,
        KeyboardMouseSettings.Off);
}
