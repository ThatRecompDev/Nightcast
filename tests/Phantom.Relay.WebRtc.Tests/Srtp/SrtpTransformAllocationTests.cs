// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Testing.Allocations;
using Phantom.Relay.WebRtc.Srtp;

namespace Phantom.Relay.WebRtc.Tests.Srtp;

/// <summary>The per-packet work of both profiles allocates nothing once warm.</summary>
public sealed class SrtpTransformAllocationTests
{
    private static readonly byte[] MasterKey = Convert.FromHexString("E1F97A0D3E018BE0D64FA32C06DE4139");

    [Theory]
    [InlineData(SrtpProfile.AesCm128HmacSha1Tag80, 14)]
    [InlineData(SrtpProfile.AeadAes128Gcm, 12)]
    public void Protecting_and_unprotecting_rtp_and_rtcp_allocates_nothing(SrtpProfile profile, int saltLength)
    {
        var keys = SrtpSessionKeys.Derive(profile, MasterKey, new byte[saltLength]);
        var transform = SrtpTransform.Create(profile, keys);
        var rtp = new byte[1300];
        var rtcp = new byte[100];
        rtp[0] = 0x80;
        rtcp[0] = 0x81;

        var report = AllocationMeter.Measure(() => RoundTrip(transform, rtp, rtcp));

        Assert.Equal(0, report.BytesPerRun);
    }

    private static void RoundTrip(SrtpTransform transform, byte[] rtp, byte[] rtcp)
    {
        Assert.True(transform.TryProtectRtp(rtp, 1200, 12, 0, out int length));
        Assert.True(transform.TryUnprotectRtp(rtp.AsSpan(0, length), 12, 0, out _));
        Assert.True(transform.TryProtectRtcp(rtcp, 32, 1, out length));
        Assert.True(transform.TryUnprotectRtcp(rtcp.AsSpan(0, length), out _));
    }
}
