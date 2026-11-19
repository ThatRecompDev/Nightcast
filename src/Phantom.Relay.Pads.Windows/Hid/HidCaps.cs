// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;

namespace Phantom.Relay.Pads.Windows.Hid;

/// <summary>
/// <c>HIDP_CAPS</c>: the top-level collection's usage and the longest report of each kind, report id included, then
/// seventeen reserved words and ten counts this reader does not need.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct HidCaps
{
    /// <summary>The words after the report lengths: seventeen reserved, then ten counts of capabilities.</summary>
    public const int RestWords = 27;

    public ushort Usage;
    public ushort UsagePage;
    public ushort InputReportByteLength;
    public ushort OutputReportByteLength;
    public ushort FeatureReportByteLength;
    public fixed ushort Rest[RestWords];
}
