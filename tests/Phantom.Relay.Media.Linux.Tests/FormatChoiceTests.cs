// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Media.Linux.Interop;

namespace Phantom.Relay.Media.Linux.Tests;

public sealed class FormatChoiceTests
{
    [Fact]
    public void VaapiSurfacesAreTakenWhenOffered()
    {
        int[] offered = [FfmpegLayout.PixelFormatYuv420P, FfmpegLayout.PixelFormatVaapi];

        Assert.Equal(FfmpegLayout.PixelFormatVaapi, FormatChoice.Choose(offered));
    }

    [Fact]
    public void SoftwarePlanesAreTakenWhenTheHwaccelCannot()
    {
        int[] offered = [FfmpegLayout.PixelFormatNv12, FfmpegLayout.PixelFormatYuvj420P];

        Assert.Equal(FfmpegLayout.PixelFormatYuvj420P, FormatChoice.Choose(offered));
    }

    [Fact]
    public void NothingUsableIsRefused()
    {
        int[] offered = [FfmpegLayout.PixelFormatNv12];

        Assert.Equal(FfmpegLayout.PixelFormatNone, FormatChoice.Choose(offered));
        Assert.Equal(FfmpegLayout.PixelFormatNone, FormatChoice.Choose([]));
    }
}
