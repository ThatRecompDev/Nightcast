// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Phantom.Relay.App;
using Phantom.Relay.Stream.Setup;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>
/// Fullscreen on Linux (X11, which SteamOS's gamescope serves through XWayland): the window manager is asked for
/// _NET_WM_STATE_FULLSCREEN, the standard request sokol_app itself makes, and the window is sized to the screen, so
/// gamescope gets the screen's own resolution instead of scaling a small window up. The engine's window has no
/// fullscreen call yet, so the window is found through a connection of its own, by its title (the app's name); an engine
/// candidate. Without an X server or the window it stays a window.
/// </summary>
[SupportedOSPlatform("linux")]
public sealed class X11Fullscreen : IWindowMode, IDisposable
{
    private const string StateAtom = "_NET_WM_STATE";
    private const string FullscreenAtom = "_NET_WM_STATE_FULLSCREEN";

    private readonly bool _startFullscreen;
    private nint _display;
    private nint _window;
    private bool _begun;

    /// <summary>The window goes fullscreen at <see cref="Begin"/> when <paramref name="startFullscreen"/>.</summary>
    public X11Fullscreen(bool startFullscreen)
    {
        _startFullscreen = startFullscreen;
    }

    public bool Fullscreen { get; private set; }

    /// <summary>Once, when the window exists; later calls change nothing.</summary>
    public void Begin()
    {
        if (_begun)
        {
            return;
        }

        _begun = true;
        _display = X11Methods.XOpenDisplay(0);

        if (_display == 0)
        {
            return;
        }

        _window = FindWindow(_display, X11Methods.XDefaultRootWindow(_display));

        if (_startFullscreen)
        {
            Set(true);
        }
    }

    public void Toggle()
    {
        Set(!Fullscreen);
    }

    public void Dispose()
    {
        if (_display != 0)
        {
            X11Methods.XCloseDisplay(_display);
            _display = 0;
        }
    }

    private void Set(bool fullscreen)
    {
        if (_display == 0 || _window == 0)
        {
            return;
        }

        nint action = X11Methods.StateRemove;

        if (fullscreen)
        {
            action = X11Methods.StateAdd;
        }

        var message = new X11ClientMessage
        {
            Type = X11ClientMessage.ClientMessageType,
            SendEvent = 1,
            Display = _display,
            Window = _window,
            MessageType = X11Methods.XInternAtom(_display, StateAtom, 0),
            Format = X11ClientMessage.LongFormat,
            Data0 = action,
            Data1 = X11Methods.XInternAtom(_display, FullscreenAtom, 0),
            Data3 = X11Methods.NormalSource,
        };

        X11Methods.XSendEvent(_display, X11Methods.XDefaultRootWindow(_display), 0, X11Methods.WindowManagerMask, ref message);
        FillScreen(fullscreen);
        X11Methods.XFlush(_display);
        Fullscreen = fullscreen;
    }

    /// <summary>Fullscreen, the window takes the screen's size too, for a window manager that only honours that.</summary>
    private void FillScreen(bool fullscreen)
    {
        if (!fullscreen)
        {
            return;
        }

        int screen = X11Methods.XDefaultScreen(_display);
        uint width = (uint)X11Methods.XDisplayWidth(_display, screen);
        uint height = (uint)X11Methods.XDisplayHeight(_display, screen);
        X11Methods.XMoveResizeWindow(_display, _window, 0, 0, width, height);
    }

    /// <summary>The first window below <paramref name="parent"/>, depth first, whose title starts with the app's name.</summary>
    private static nint FindWindow(nint display, nint parent)
    {
        if (X11Methods.XQueryTree(display, parent, out _, out _, out nint children, out uint count) == 0 || children == 0)
        {
            return 0;
        }

        try
        {
            for (int index = 0; index < count; index++)
            {
                nint child = Marshal.ReadIntPtr(children, index * IntPtr.Size);
                nint found = child;

                if (!Named(display, child))
                {
                    found = FindWindow(display, child);
                }

                if (found != 0)
                {
                    return found;
                }
            }

            return 0;
        }
        finally
        {
            X11Methods.XFree(children);
        }
    }

    private static bool Named(nint display, nint window)
    {
        if (X11Methods.XFetchName(display, window, out nint name) == 0 || name == 0)
        {
            return false;
        }

        string? title = Marshal.PtrToStringUTF8(name);
        X11Methods.XFree(name);

        return title is not null && title.StartsWith(RelayApp.Name, StringComparison.Ordinal);
    }
}
