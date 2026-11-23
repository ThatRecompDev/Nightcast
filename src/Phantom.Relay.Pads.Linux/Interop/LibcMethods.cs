// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Phantom.Relay.Pads.Linux.Interop;

/// <summary>
/// The C library's file calls an event device is read, written and asked with. The library is glibc's
/// <c>libc.so.6</c> (SteamOS is Arch Linux, a glibc system). The flag values are Linux's generic ones (x86-64 and
/// arm64 share them); errors come back as <see cref="Marshal.GetLastPInvokeError"/> (errno).
/// </summary>
[SupportedOSPlatform("linux")]
internal static unsafe partial class LibcMethods
{
    public const int ReadOnly = 0x0;
    public const int ReadWrite = 0x2;
    public const int NonBlocking = 0x800;
    public const int CloseOnExec = 0x80000;

    /// <summary><c>POLLIN</c>, <c>POLLERR</c>, <c>POLLHUP</c> and <c>POLLNVAL</c>.</summary>
    public const short PollIn = 0x1;
    public const short PollError = 0x8;
    public const short PollHangUp = 0x10;
    public const short PollInvalid = 0x20;

    /// <summary><c>EINTR</c> and <c>EAGAIN</c>: a call that was interrupted, or would have waited.</summary>
    public const int Interrupted = 4;
    public const int WouldBlock = 11;

    private const string Library = "libc.so.6";

    [LibraryImport(Library, EntryPoint = "open", SetLastError = true, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int Open(string path, int flags);

    [LibraryImport(Library, EntryPoint = "close", SetLastError = true)]
    public static partial int Close(int descriptor);

    [LibraryImport(Library, EntryPoint = "read", SetLastError = true)]
    public static partial nint Read(int descriptor, void* buffer, nuint count);

    [LibraryImport(Library, EntryPoint = "write", SetLastError = true)]
    public static partial nint Write(int descriptor, void* buffer, nuint count);

    [LibraryImport(Library, EntryPoint = "ioctl", SetLastError = true)]
    public static partial int Ioctl(int descriptor, nuint request, void* argument);

    [LibraryImport(Library, EntryPoint = "poll", SetLastError = true)]
    public static partial int Poll(PollDescriptor* descriptors, nuint count, int timeoutMilliseconds);

    [LibraryImport(Library, EntryPoint = "eventfd", SetLastError = true)]
    public static partial int EventDescriptor(uint initial, int flags);
}
