// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Phantom.Relay.App;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// Fullscreen on Windows as a borderless window over the whole monitor (the swap chain keeps flipping, so the
/// compositor can hand the screen straight to it): the frame is taken off sokol's window and the window set to its
/// monitor's area; windowed again, the frame and the place it had come back. The engine's window has no fullscreen call
/// yet, so the window is found by sokol's window class in this process. Game-agnostic: an engine candidate.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class BorderlessFullscreen : IWindowMode
{
    /// <summary>NOT DATA: the window class sokol_app registers on Windows.</summary>
    private const string SokolWindowClass = "SOKOLAPP";

    private readonly bool _startFullscreen;
    private nint _window;
    private nint _windowedStyle;
    private Win32Rect _windowedPlace;

    /// <summary>The window goes fullscreen at <see cref="Begin"/> when <paramref name="startFullscreen"/>.</summary>
    public BorderlessFullscreen(bool startFullscreen)
    {
        _startFullscreen = startFullscreen;
    }

    public bool Fullscreen { get; private set; }

    public void Begin()
    {
        _window = FindOwnWindow();

        if (_startFullscreen)
        {
            Enter();
        }
    }

    public void Toggle()
    {
        if (Fullscreen)
        {
            Leave();
        }
        else
        {
            Enter();
        }
    }

    private void Enter()
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        var monitor = User32Methods.MonitorFromWindow(_window, User32Methods.NearestMonitor);

        if (_window == 0 || User32Methods.GetWindowRect(_window, out _windowedPlace) == 0 || User32Methods.GetMonitorInfo(monitor, ref info) == 0)
        {
            return;
        }

        _windowedStyle = User32Methods.GetWindowLongPtr(_window, User32Methods.StyleIndex);
        nint borderless = (nint)(((long)_windowedStyle & ~User32Methods.OverlappedWindow) | User32Methods.Popup);
        User32Methods.SetWindowLongPtr(_window, User32Methods.StyleIndex, borderless);

        var area = info.Monitor;
        Fullscreen = User32Methods.SetWindowPos(_window, 0, area.Left, area.Top, area.Width, area.Height, User32Methods.FrameChanged | User32Methods.KeepOwnerOrder) != 0;
    }

    private void Leave()
    {
        User32Methods.SetWindowLongPtr(_window, User32Methods.StyleIndex, _windowedStyle);

        var place = _windowedPlace;
        uint flags = User32Methods.FrameChanged | User32Methods.KeepOrder | User32Methods.KeepOwnerOrder;
        Fullscreen = User32Methods.SetWindowPos(_window, 0, place.Left, place.Top, place.Width, place.Height, flags) == 0;
    }

    /// <summary>Sokol's window in this process; 0 when there is none.</summary>
    private static nint FindOwnWindow()
    {
        uint own = (uint)Environment.ProcessId;
        nint window = 0;

        while ((window = User32Methods.FindWindowEx(0, window, SokolWindowClass, null)) != 0)
        {
            User32Methods.GetWindowThreadProcessId(window, out uint process);

            if (process == own)
            {
                return window;
            }
        }

        return 0;
    }
}
