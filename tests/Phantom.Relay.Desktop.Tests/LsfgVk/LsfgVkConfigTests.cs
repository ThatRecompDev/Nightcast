// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkConfigTests
{
    private const string Dll = "/data/player/Steam/steamapps/common/Lossless Scaling/lsfg-vk.dll";
    private const string Log = "/data/player/Nightcast/lsfg-vk/lsfg-vk.log";

    [Fact]
    public void The_profile_named_Nightcast_carries_the_players_multiplier_flow_and_shaders()
    {
        var settings = FrameGeneration.QualityDoubled with { Mode = FrameGenerationMode.Performance, Multiplier = 3, FlowScale = 0.5f };

        Assert.Equal(
            "version = 2\n\n" +
            "[global]\n" +
            $"dll = \"{Dll}\"\n" +
            "allow_fp16 = true\n" +
            $"log_file = \"{Log}\"\n\n" +
            "[[profile]]\n" +
            "name = \"Nightcast\"\n" +
            "pacing_mode = \"vsync\"\n" +
            "multiplier = 3\n" +
            "flow_scale = 0.5\n" +
            "performance_mode = true\n",
            LsfgVkConfig.Text(settings, Dll, Log));
    }

    [Fact]
    public void Off_is_multiplier_1_which_lsfg_vk_passes_through_and_the_flow_stays_in_its_range()
    {
        var text = LsfgVkConfig.Text(FrameGeneration.Off with { FlowScale = 0.1f }, Dll, Log);

        Assert.Contains("multiplier = 1\n", text, StringComparison.Ordinal);
        Assert.Contains("flow_scale = 0.25\n", text, StringComparison.Ordinal);
        Assert.Contains("performance_mode = false\n", text, StringComparison.Ordinal);
        Assert.Contains("flow_scale = 1.0\n", LsfgVkConfig.Text(FrameGeneration.QualityDoubled, Dll, Log), StringComparison.Ordinal);
        Assert.Equal(4, LsfgVkConfig.Multiplier(FrameGeneration.QualityDoubled with { Multiplier = 9 }));
    }

    [Fact]
    public void Strings_are_TOML_basic_strings_with_quotes_backslashes_and_control_characters_escaped()
    {
        Assert.Equal("\"a \\\"b\\\" c\\\\d\\u0009e\\u007F\"", LsfgVkConfig.Quoted("a \"b\" c\\d\te\u007f"));
    }

    [Fact]
    public void Saving_replaces_the_whole_file_and_leaves_nothing_beside_it()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "conf.toml");
        File.WriteAllText(path, "old, and longer than the new text");

        LsfgVkConfig.Save(path, "new");

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(folder.Path));
    }
}
