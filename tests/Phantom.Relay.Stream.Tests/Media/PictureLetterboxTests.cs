// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

public sealed class PictureLetterboxTests
{
    private const float Tolerance = 0.0001f;

    [Fact]
    public void A_16_by_10_screen_gets_equal_bars_above_and_below()
    {
        var rectangle = PictureLetterbox.Of(1920, 1200);

        // 1080 of 1200 lines: 60 above, 60 below.
        AssertNear(new Vector4(0f, 0.05f, 1f, 0.95f), rectangle);
    }

    [Fact]
    public void A_wider_screen_gets_equal_bars_left_and_right()
    {
        var rectangle = PictureLetterbox.Of(2560, 1080);

        float width = 1920f / 2560f;
        AssertNear(new Vector4((1f - width) / 2f, 0f, (1f + width) / 2f, 1f), rectangle);
    }

    [Fact]
    public void A_16_by_9_window_is_filled_and_stretch_fills_any_window()
    {
        AssertNear(new Vector4(0f, 0f, 1f, 1f), PictureLetterbox.Of(1280, 720));
        AssertNear(new Vector4(0f, 0f, 1f, 1f), PictureLetterbox.Of(1920, 1200, PictureFit.Stretch));
    }

    private static void AssertNear(Vector4 expected, Vector4 actual)
    {
        Assert.True(Vector4.Distance(expected, actual) < Tolerance, $"expected {expected}, got {actual}");
    }
}
