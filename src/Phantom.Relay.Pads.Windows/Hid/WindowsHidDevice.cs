// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using Phantom.Relay.Pads.Windows.Devices;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>
/// One HID device opened for overlapped reads and writes. A read stays started between calls, so no report is missed
/// while the reader writes; its <c>OVERLAPPED</c> and buffers live in native memory, which the system may write until
/// the read is finished or cancelled, and are freed only after. A read waits on the read's event and on a wake event
/// together, so <see cref="Wake"/> ends a wait at once. A write waits for its own completion a short while at most.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed unsafe class WindowsHidDevice : IHidDevice
{
    /// <summary>NOT DATA: the longest a read waits without a report or a wake (a pad sends one every few milliseconds).</summary>
    private const int ReadWaitMilliseconds = 250;

    /// <summary>NOT DATA: the longest a write may take before it is cancelled.</summary>
    private const int WriteWaitMilliseconds = 100;

    private const int ReadSignal = 0;

    private readonly SafeFileHandle _handle;
    private readonly ManualResetEvent _readDone = new(false);
    private readonly ManualResetEvent _writeDone = new(false);
    private readonly AutoResetEvent _wake = new(false);
    private readonly WaitHandle[] _waits;
    private readonly NativeOverlapped* _readOverlapped;
    private readonly NativeOverlapped* _writeOverlapped;
    private readonly byte* _readBuffer;
    private readonly byte* _writeBuffer;
    private bool _reading;
    private bool _disposed;

    private WindowsHidDevice(SafeFileHandle handle, HidCaps caps)
    {
        _handle = handle;
        _waits = [_readDone, _wake];
        InputReportLength = caps.InputReportByteLength;
        OutputReportLength = caps.OutputReportByteLength;
        FeatureReportLength = caps.FeatureReportByteLength;

        // Native, so neither moves while the system reads or writes them.
        _readOverlapped = (NativeOverlapped*)NativeMemory.AllocZeroed((nuint)sizeof(NativeOverlapped));
        _writeOverlapped = (NativeOverlapped*)NativeMemory.AllocZeroed((nuint)sizeof(NativeOverlapped));
        _readBuffer = (byte*)NativeMemory.AllocZeroed((nuint)Math.Max(InputReportLength, 1));
        _writeBuffer = (byte*)NativeMemory.AllocZeroed((nuint)Math.Max(OutputReportLength, 1));
    }

    public int InputReportLength { get; }

    public int OutputReportLength { get; }

    public int FeatureReportLength { get; }

    /// <summary>Opens the device at <paramref name="path"/> for reading and writing; null when it cannot be opened.</summary>
    public static WindowsHidDevice? TryOpen(string path)
    {
        var handle = Kernel32Methods.CreateFile(
            path,
            Kernel32Methods.GenericRead | Kernel32Methods.GenericWrite,
            Kernel32Methods.ShareRead | Kernel32Methods.ShareWrite,
            0,
            Kernel32Methods.OpenExisting,
            Kernel32Methods.FlagOverlapped,
            0);

        if (handle.IsInvalid || !TryCaps(handle, out var caps) || caps.InputReportByteLength == 0)
        {
            handle.Dispose();

            return null;
        }

        return new WindowsHidDevice(handle, caps);
    }

    public HidReadResult Read(Span<byte> report, out int length)
    {
        length = 0;

        if (!_reading && !BeginRead())
        {
            return HidReadResult.Gone;
        }

        if (WaitHandle.WaitAny(_waits, ReadWaitMilliseconds) != ReadSignal)
        {
            return HidReadResult.Idle;
        }

        _reading = false;

        if (!Kernel32Methods.GetOverlappedResult(_handle, _readOverlapped, out var transferred, false))
        {
            return HidReadResult.Gone;
        }

        length = Math.Min((int)transferred, report.Length);
        new ReadOnlySpan<byte>(_readBuffer, length).CopyTo(report);

        return HidReadResult.Report;
    }

    public void Wake()
    {
        _wake.Set();
    }

    public bool ReadFeature(Span<byte> report)
    {
        fixed (byte* buffer = report)
        {
            return HidMethods.GetFeature(_handle, buffer, (uint)report.Length);
        }
    }

    public bool Write(ReadOnlySpan<byte> report)
    {
        if (report.Length > OutputReportLength)
        {
            return false;
        }

        // Windows wants the device's longest output report: the rest is zeros.
        var buffer = new Span<byte>(_writeBuffer, OutputReportLength);
        buffer.Clear();
        report.CopyTo(buffer);
        *_writeOverlapped = default;
        _writeOverlapped->EventHandle = _writeDone.SafeWaitHandle.DangerousGetHandle();

        if (!Kernel32Methods.WriteFile(_handle, _writeBuffer, (uint)OutputReportLength, 0, _writeOverlapped)
            && Marshal.GetLastPInvokeError() != Kernel32Methods.ErrorIoPending)
        {
            return false;
        }

        if (!_writeDone.WaitOne(WriteWaitMilliseconds))
        {
            Kernel32Methods.CancelIoEx(_handle, _writeOverlapped);
        }

        return Kernel32Methods.GetOverlappedResult(_handle, _writeOverlapped, out _, true);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // The system may write a started read until it is cancelled and done: only then is its memory freed.
        if (_reading)
        {
            Kernel32Methods.CancelIoEx(_handle, _readOverlapped);
            Kernel32Methods.GetOverlappedResult(_handle, _readOverlapped, out _, true);
        }

        _handle.Dispose();
        _readDone.Dispose();
        _writeDone.Dispose();
        _wake.Dispose();
        FreeNative();
    }

    private static bool TryCaps(SafeFileHandle handle, out HidCaps caps)
    {
        caps = default;

        if (!HidMethods.GetPreparsedData(handle, out var preparsed))
        {
            return false;
        }

        var status = HidMethods.GetCaps(preparsed, out caps);
        HidMethods.FreePreparsedData(preparsed);

        return status == HidMethods.Success;
    }

    /// <summary>Starts the next read; false when the device refused it (it is gone).</summary>
    private bool BeginRead()
    {
        *_readOverlapped = default;
        _readOverlapped->EventHandle = _readDone.SafeWaitHandle.DangerousGetHandle();

        if (!Kernel32Methods.ReadFile(_handle, _readBuffer, (uint)InputReportLength, 0, _readOverlapped)
            && Marshal.GetLastPInvokeError() != Kernel32Methods.ErrorIoPending)
        {
            return false;
        }

        _reading = true;

        return true;
    }

    private void FreeNative()
    {
        NativeMemory.Free(_readOverlapped);
        NativeMemory.Free(_writeOverlapped);
        NativeMemory.Free(_readBuffer);
        NativeMemory.Free(_writeBuffer);
    }
}
