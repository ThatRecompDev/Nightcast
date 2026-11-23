// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>The Xlib calls the Linux fullscreen needs, source-generated (NativeAOT-safe); libX11 is the system's.</summary>
internal static partial class X11Methods
{
    private const string Library = "libX11.so.6";

    /// <summary>SubstructureRedirectMask | SubstructureNotifyMask: how a window manager is asked to change a window.</summary>
    public const nint WindowManagerMask = (1 << 20) | (1 << 19);

    /// <summary>_NET_WM_STATE_ADD and _NET_WM_STATE_REMOVE.</summary>
    public const nint StateAdd = 1;

    public const nint StateRemove = 0;

    /// <summary>The request comes from a normal application (EWMH source indication).</summary>
    public const nint NormalSource = 1;

    [LibraryImport(Library)]
    public static partial nint XOpenDisplay(nint name);

    [LibraryImport(Library)]
    public static partial void XCloseDisplay(nint display);

    [LibraryImport(Library)]
    public static partial nint XDefaultRootWindow(nint display);

    [LibraryImport(Library)]
    public static partial int XDefaultScreen(nint display);

    [LibraryImport(Library)]
    public static partial int XDisplayWidth(nint display, int screen);

    [LibraryImport(Library)]
    public static partial int XDisplayHeight(nint display, int screen);

    [LibraryImport(Library, StringMarshalling = StringMarshalling.Utf8)]
    public static partial nint XInternAtom(nint display, string name, int onlyIfExists);

    [LibraryImport(Library)]
    public static partial int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint children, out uint count);

    [LibraryImport(Library)]
    public static partial int XFetchName(nint display, nint window, out nint name);

    [LibraryImport(Library)]
    public static partial void XFree(nint data);

    [LibraryImport(Library)]
    public static partial int XSendEvent(nint display, nint window, int propagate, nint mask, ref X11ClientMessage message);

    [LibraryImport(Library)]
    public static partial void XMoveResizeWindow(nint display, nint window, int x, int y, uint width, uint height);

    [LibraryImport(Library)]
    public static partial void XFlush(nint display);
}
