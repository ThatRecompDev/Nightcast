// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Media.Linux.Tests;

public sealed class PlaneCopiesTests
{
    [Fact]
    public void Nv12WithItsOwnChromaPitchIsSplitIntoPlanes()
    {
        // 4 x 2 luma with pitch 6; one chroma row of two pairs with pitch 8 (wider than luma's, as a driver may give).
        byte[] luma = [1, 2, 3, 4, 0, 0, 5, 6, 7, 8];
        byte[] chroma = [10, 20, 11, 21];
        var frame = new VideoFrame();

        PlaneCopies.CopyNv12(luma, 6, chroma, 8, 4, 2, frame);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], frame.Luma.ToArray());
        Assert.Equal([10, 11], frame.Cb.ToArray());
        Assert.Equal([20, 21], frame.Cr.ToArray());
    }

    [Fact]
    public void ThreePlanesAreCopiedRowByRow()
    {
        // 2 x 4 luma with pitch 3; chroma 1 x 2 with pitch 2.
        byte[] luma = [1, 2, 0, 3, 4, 0, 5, 6, 0, 7, 8];
        byte[] cb = [9, 0, 10];
        byte[] cr = [11, 0, 12];
        var frame = new VideoFrame();

        PlaneCopies.CopyPlanar(luma, 3, cb, cr, 2, 2, 4, frame);

        Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], frame.Luma.ToArray());
        Assert.Equal([9, 10], frame.Cb.ToArray());
        Assert.Equal([11, 12], frame.Cr.ToArray());
    }

    [Fact]
    public void AShortPlaneIsRefused()
    {
        byte[] luma = [1, 2, 3];
        byte[] chroma = [10, 20];
        var frame = new VideoFrame();

        Assert.Throws<ArgumentOutOfRangeException>(() => PlaneCopies.CopyNv12(luma, 2, chroma, 2, 2, 2, frame));
    }

    [Fact]
    public void ExtentEndsAtTheLastRowsWidth()
    {
        Assert.Equal((2560 * 1439) + 2560, PlaneCopies.Extent(2560, 2560, 1440));
        Assert.Equal(2560, PlaneCopies.Nv12ChromaRow(2560));
        Assert.Equal(720, PlaneCopies.ChromaRows(1440));
    }
}
