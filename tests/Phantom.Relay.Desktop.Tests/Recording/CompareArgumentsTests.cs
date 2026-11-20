// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.Recording;

namespace Phantom.Relay.Desktop.Tests.Recording;

public sealed class CompareArgumentsTests
{
    private static readonly CompareRequest Request = new(
        Path.Combine("rec", "source.mp4"),
        Path.Combine("rec", "shown.mp4"),
        Path.Combine("rec", "compare.mp4"),
        TimeSpan.FromMilliseconds(34),
        1920,
        1080,
        60,
        "console 30 fps",
        "Relay LSFG x2",
        @"C:\Windows\Fonts\consola.ttf",
        HalfSpeed: false);

    [Fact]
    public void Each_argument_is_its_own_entry_with_the_shown_file_started_later_and_the_output_last()
    {
        var arguments = CompareArguments.Build(Request);

        var shownInput = IndexOfPair(arguments, "-i", Request.Shown);
        Assert.Equal(Request.Source, arguments[IndexOfPair(arguments, "-i", Request.Source) + 1]);
        Assert.Equal(["-itsoffset", "0.034000"], arguments.Skip(shownInput - 2).Take(2));
        Assert.Equal(Request.Output, arguments[^1]);
        Assert.Contains("pipe:1", arguments);
    }

    [Fact]
    public void A_pair_narrower_than_nvencs_h264_limit_is_encoded_as_h264()
    {
        var arguments = CompareArguments.Build(Request);

        Assert.Equal(CompareArguments.H264Encoder, arguments[IndexOfPair(arguments, "-c:v", CompareArguments.H264Encoder) + 1]);
        Assert.DoesNotContain("hvc1", arguments);
    }

    [Fact]
    public void Two_1440p_pictures_side_by_side_are_too_wide_for_it_and_go_to_hevc()
    {
        var arguments = CompareArguments.Build(Request with { Width = 2560, Height = 1440 });

        Assert.Contains(CompareArguments.HevcEncoder, arguments);
        Assert.Contains("hvc1", arguments);
    }

    [Fact]
    public void The_graph_puts_the_source_left_and_the_scaled_shown_pictures_right_both_labelled_at_one_rate()
    {
        var graph = CompareArguments.Graph(Request);

        Assert.Equal(
            "[0:v]fps=60,drawtext=fontfile='C\\:/Windows/Fonts/consola.ttf':text='console 30 fps':x=22:y=22:fontsize=30:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=9[a];"
            + "[1:v]scale=1920:1080,fps=60,drawtext=fontfile='C\\:/Windows/Fonts/consola.ttf':text='Relay LSFG x2':x=22:y=22:fontsize=30:fontcolor=white:box=1:boxcolor=black@0.6:boxborderw=9[b];"
            + "[a][b]hstack=inputs=2:shortest=1[v]",
            graph);
    }

    [Fact]
    public void Half_speed_doubles_the_times_after_the_stack()
    {
        var graph = CompareArguments.Graph(Request with { HalfSpeed = true });

        Assert.EndsWith("hstack=inputs=2:shortest=1,setpts=2*PTS[v]", graph, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_font_file_ffmpeg_uses_its_own()
    {
        var graph = CompareArguments.Graph(Request with { FontFile = null });

        Assert.DoesNotContain("fontfile", graph, StringComparison.Ordinal);
    }

    [Fact]
    public void A_path_with_a_quote_cannot_be_a_filter_value()
    {
        Assert.Null(CompareArguments.FilterPath(@"C:\it's\font.ttf"));
        Assert.Equal("'D\\:/fonts/a.ttf'", CompareArguments.FilterPath(@"D:\fonts\a.ttf"));
    }

    [Fact]
    public void A_label_keeps_only_plain_characters_drawtext_cannot_misread()
    {
        Assert.Equal("Relay LSFG x2 (quality 50 flow 1.0)", CompareArguments.LabelText("Relay LSFG x2 (quality: 50%, flow 1.0)'"));
    }

    [Fact]
    public void A_negative_offset_starts_both_together()
    {
        var arguments = CompareArguments.Build(Request with { ShownOffset = TimeSpan.FromMilliseconds(-5) });

        Assert.Equal("0.000000", arguments[IndexOfPair(arguments, "-itsoffset", "0.000000") + 1]);
    }

    private static int IndexOfPair(IReadOnlyList<string> arguments, string option, string value)
    {
        for (var index = 0; index + 1 < arguments.Count; index++)
        {
            if (arguments[index] == option && arguments[index + 1] == value)
            {
                return index;
            }
        }

        Assert.Fail($"no {option} {value}");

        return -1;
    }
}
