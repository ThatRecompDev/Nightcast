// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.Versioning;
using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Graphics.Sokol;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Logging;
using Phantom.Engine.Platform.Desktop.Fonts;
using Phantom.Engine.Platform.Desktop.Gamepads;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App;
using Phantom.Relay.App.Input;
using Phantom.Relay.Desktop.Fonts;
using Phantom.Relay.Pads.Linux;
using Phantom.Relay.Pads.Windows;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// The desktop's window on this thread until it closes, with the platform's pads, fonts and fullscreen: on Windows Xbox
/// pads through XInput and PlayStation pads through HID (as one), Windows' fonts and its borderless fullscreen; on Linux
/// every pad through evdev (on SteamOS Steam Input's virtual pad, the pads Steam lists as its own left alone), the fonts
/// under fontconfig's folders, and X11's fullscreen at the screen's own size (so gamescope does not scale a small
/// window up); the mode takes effect on the first screen (<see cref="WindowModeStart"/>); elsewhere no
/// pads and no fonts. The head's <c>build</c> makes the application from those parts; the log is drained after every frame.
/// </summary>
internal static class DesktopWindow
{
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;
    private const string HomeVariable = "HOME";
    private const string DataHomeVariable = "XDG_DATA_HOME";

    public static void Run(Func<WindowParts, IApplication> build, bool fullscreen, LogPump pump)
    {
        if (OperatingSystem.IsWindows())
        {
            RunWithWindowsPads(build, fullscreen, pump);

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            RunWithLinuxPads(build, fullscreen, pump);

            return;
        }

        Run(build, new NoGamepads(), new NoSystemFonts(), new WindowedOnly(), pump);
    }

    /// <summary>The fonts under fontconfig's folders, the user's own first.</summary>
    public static LinuxSystemFonts LinuxFonts()
    {
        return new LinuxSystemFonts(LinuxSystemFonts.Folders(Environment.GetEnvironmentVariable(HomeVariable), Environment.GetEnvironmentVariable(DataHomeVariable)));
    }

    /// <summary>Linux's pads (evdev) and fonts, and its X11 fullscreen, started in it when <paramref name="fullscreen"/>.</summary>
    [SupportedOSPlatform("linux")]
    private static void RunWithLinuxPads(Func<WindowParts, IApplication> build, bool fullscreen, LogPump pump)
    {
        using var pads = new EvdevPads(Environment.GetEnvironmentVariable(EvdevPads.IgnoreVariable));
        using var mode = new X11Fullscreen(fullscreen);
        pads.Start();
        Run(build, pads, LinuxFonts(), mode, pump);
        pads.Stop();
    }

    /// <summary>Windows' pads and fonts, and its borderless fullscreen, started in it when <paramref name="fullscreen"/>.</summary>
    [SupportedOSPlatform("windows")]
    private static void RunWithWindowsPads(Func<WindowParts, IApplication> build, bool fullscreen, LogPump pump)
    {
        using var playStation = new PlayStationPads();
        playStation.Start();

        var xbox = new GamepadsWithFeedback(new XInputGamepads(), new XInputMotors());
        var pads = new CombinedGamepads(xbox, playStation, InputLimits.DefaultGamepads);
        Run(build, pads, new WindowsSystemFonts(), new BorderlessFullscreen(fullscreen), pump);
        playStation.Stop();
    }

    /// <summary>The window and its frames until it closes, with <paramref name="pads"/> read once a frame.</summary>
    private static void Run(Func<WindowParts, IApplication> build, IGamepadSource pads, ISystemFonts fonts, IWindowMode mode, LogPump pump)
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var application = build(new WindowParts(device, window, fonts, pads as IGamepadFeedback, mode));
        var started = new WindowModeStart(application, mode);
        var driver = new ApplicationDriver(new LogDrainingApplication(started, pump), new InputFrame(InputLimits.Default), pads);

        SokolRunner.Run(driver, new SokolWindow(WindowWidth, WindowHeight, RelayApp.Name), SokolLimits.Default);
    }
}
