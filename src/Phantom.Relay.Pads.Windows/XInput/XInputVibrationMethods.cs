// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Windows.XInput;

/// <summary>XInput's motor call, source-generated (NativeAOT-safe).</summary>
internal static partial class XInputVibrationMethods
{
    /// <summary><c>ERROR_SUCCESS</c>: the pad took the speeds.</summary>
    public const uint Success = 0;

    /// <summary><c>XUSER_MAX_COUNT</c>: the users XInput serves.</summary>
    public const int Users = 4;

    /// <summary><c>XInputSetState</c>: the user's motor speeds, or <c>ERROR_DEVICE_NOT_CONNECTED</c> with no pad.</summary>
    [LibraryImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    public static partial uint SetState(uint user, ref XInputVibration vibration);
}
