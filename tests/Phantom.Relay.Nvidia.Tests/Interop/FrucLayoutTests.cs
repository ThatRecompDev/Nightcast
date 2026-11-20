// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Nvidia.Interop;

namespace Phantom.Relay.Nvidia.Tests.Interop;

/// <summary>The hand-written structures have NvOFFRUC.h's sizes and offsets on x64 (counted from the header by hand).</summary>
public sealed class FrucLayoutTests
{
    [Fact]
    public unsafe void The_parameter_structures_have_the_headers_sizes()
    {
        Assert.Equal(160, sizeof(FrucCreateParameters));
        Assert.Equal(160, sizeof(FrucFrameData));
        Assert.Equal(312, sizeof(FrucProcessInput));
        Assert.Equal(304, sizeof(FrucProcessOutput));
        Assert.Equal(96, sizeof(FrucRegistration));
        Assert.Equal(88, sizeof(FrucUnregistration));
    }

    [Fact]
    public void The_synchronisation_values_sit_where_the_unions_start()
    {
        Assert.Equal(168, (int)Marshal.OffsetOf<FrucProcessInput>(nameof(FrucProcessInput.WaitValue)));
        Assert.Equal(160, (int)Marshal.OffsetOf<FrucProcessOutput>(nameof(FrucProcessOutput.SignalValue)));
        Assert.Equal(80, (int)Marshal.OffsetOf<FrucRegistration>(nameof(FrucRegistration.Fence)));
        Assert.Equal(88, (int)Marshal.OffsetOf<FrucRegistration>(nameof(FrucRegistration.Count)));
        Assert.Equal(24, (int)Marshal.OffsetOf<FrucFrameData>(nameof(FrucFrameData.RepetitionOccurred)));
    }

    [Fact]
    public void A_status_reads_as_the_headers_name()
    {
        Assert.Equal("invalid parameter (3)", FrucStatus.Describe(3));
        Assert.Equal("status 99", FrucStatus.Describe(99));
    }
}
