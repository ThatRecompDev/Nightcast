// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Desktop.Windowing;

/// <summary>The user32 calls the borderless fullscreen needs, source-generated (NativeAOT-safe).</summary>
internal static partial class User32Methods
{
    private const string Library = "user32.dll";

    /// <summary>GWL_STYLE: the window's style bits.</summary>
    public const int StyleIndex = -16;

    /// <summary>WS_OVERLAPPEDWINDOW: the caption, the frame, the system menu and the size buttons.</summary>
    public const long OverlappedWindow = 0x00CF0000;

    /// <summary>WS_POPUP: a window without a frame.</summary>
    public const long Popup = 0x80000000;

    /// <summary>MONITOR_DEFAULTTONEAREST.</summary>
    public const uint NearestMonitor = 2;

    /// <summary>SWP_NOZORDER.</summary>
    public const uint KeepOrder = 0x0004;

    /// <summary>SWP_FRAMECHANGED: the new style's frame is drawn.</summary>
    public const uint FrameChanged = 0x0020;

    /// <summary>SWP_NOOWNERZORDER.</summary>
    public const uint KeepOwnerOrder = 0x0200;

    [LibraryImport(Library, EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint FindWindowEx(nint parent, nint after, string? className, string? title);

    [LibraryImport(Library)]
    public static partial uint GetWindowThreadProcessId(nint window, out uint processId);

    [LibraryImport(Library, EntryPoint = "GetWindowLongPtrW")]
    public static partial nint GetWindowLongPtr(nint window, int index);

    [LibraryImport(Library, EntryPoint = "SetWindowLongPtrW")]
    public static partial nint SetWindowLongPtr(nint window, int index, nint value);

    [LibraryImport(Library)]
    public static partial int GetWindowRect(nint window, out Win32Rect rect);

    [LibraryImport(Library)]
    public static partial nint MonitorFromWindow(nint window, uint flags);

    [LibraryImport(Library, EntryPoint = "GetMonitorInfoW")]
    public static partial int GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [LibraryImport(Library)]
    public static partial int SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
}
