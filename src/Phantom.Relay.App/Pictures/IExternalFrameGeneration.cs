// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Profiles;

namespace Phantom.Relay.App.Pictures;

/// <summary>
/// A frame generator outside Nightcast's own, which makes frames between the window's presents (lsfg-vk on Linux, a
/// Vulkan layer in the window's own process): the menu's frame-generation rows and F4, F5 and F8 drive it the way
/// they drive Nightcast's LSFG on Windows. While it runs the window presents only the game's new pictures, so it never
/// interpolates between two copies of one picture. The head makes it; the stream's app only tells it what the player
/// chose and shows what it says.
/// </summary>
public interface IExternalFrameGeneration
{
    /// <summary>The player's last choice, kept between runs: the stream starts with it.</summary>
    FrameGeneration Remembered { get; }

    /// <summary>True while it generates frames: it is loaded and the choice is on.</summary>
    bool Running { get; }

    /// <summary>
    /// True once it can only start with the process started again (a Vulkan layer is read when the window's Vulkan
    /// instance is made): the app then says so on screen and closes the window, and the head starts Nightcast again.
    /// </summary>
    bool RestartRequested { get; }

    /// <summary>What it is, whose it is and under which licence, shown below the menu.</summary>
    string Notice { get; }

    /// <summary>
    /// The player's choice (render thread): kept for the next run, applied at once where it can be (a small file
    /// written), else prepared in the background; it never waits for the network.
    /// </summary>
    void Use(FrameGeneration settings);

    /// <summary>
    /// The menu row's and the stats' text: what runs (<paramref name="chosen"/> is the switch's own description of the
    /// choice), or why nothing does (off, being downloaded, no Lossless Scaling, the layer not loaded).
    /// </summary>
    string Describe(string chosen);
}
