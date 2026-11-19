// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Runtime.InteropServices;
using Phantom.Relay.Lsfg.Shaders;
using Phantom.Relay.Lsfg.Upscaling;

namespace Phantom.Relay.Lsfg.Tests.Upscaling;

public sealed class UpscaleShapeTests
{
    [Fact]
    public void The_constants_struct_is_the_buffer_size()
    {
        Assert.Equal(LosslessUpscaleConstants.Size, Marshal.SizeOf<LosslessUpscaleConstants>());
    }

    [Fact]
    public void The_combine_as_reflected_is_accepted()
    {
        var layout = Combine(16, ("InputConsts1", 0), ("InputConsts2", 16), ("OutputConsts", 32));

        Assert.Null(LosslessUpscaleIds.CombineShape.Check(layout));
    }

    [Fact]
    public void A_swapped_input_or_a_moved_field_is_refused_with_what_differs()
    {
        var swapped = new ShaderLayout(146, 16, 16, [Texture("InputTexture", 0, 4), Texture("EffectTexture", 1, 1), Output(4), Sampler()], 48, []);
        var moved = Combine(16, ("OutputConsts", 16));

        Assert.Equal("shader 146 reads 4 components at t0; the upscaler expects 1", LosslessUpscaleIds.CombineShape.Check(swapped));
        Assert.Equal("shader 146 has constant 'OutputConsts' at byte 16; the upscaler expects 32", LosslessUpscaleIds.CombineShape.Check(moved));
    }

    [Fact]
    public void Another_group_size_is_refused()
    {
        Assert.NotNull(LosslessUpscaleIds.CombineShape.Check(Combine(8)));
    }

    [Theory]
    [InlineData(LosslessUpscalerKind.Ls1, 0, new[] { 147, 148, 149, 146 })]
    [InlineData(LosslessUpscalerKind.Ls1, 4, new[] { 159, 160, 161, 146 })]
    [InlineData(LosslessUpscalerKind.Ls1Performance, 2, new[] { 143, 146 })]
    [InlineData(LosslessUpscalerKind.Sgsr, 0, new[] { 250 })]
    public void Each_upscaler_runs_its_networks_ids(LosslessUpscalerKind kind, int level, int[] ids)
    {
        var settings = LosslessUpscalerSettings.Default with { Kind = kind, Level = level };

        Assert.Equal(ids, LosslessUpscaleGraph.Roles(settings).Select(static role => role.Id));
    }

    [Fact]
    public void Settings_out_of_range_are_brought_into_it()
    {
        var settings = new LosslessUpscalerSettings(LosslessUpscalerKind.Ls1, 9, -3, null!).Normalized();

        Assert.Equal((LosslessUpscalerSettings.MaximumLevel, LosslessUpscalerSettings.MinimumSharpness, string.Empty), (settings.Level, settings.Sharpness, settings.LibraryPath));
    }

    private static ShaderLayout Combine(int group, params (string Name, int Offset)[] fields)
    {
        var constants = fields.Select(static field => new ShaderConstant(field.Name, field.Offset, 16, Used: true)).ToList();

        return new ShaderLayout(146, group, group, [Texture("EffectTexture", 0, 1), Texture("InputTexture", 1, 4), Output(4), Sampler()], 48, constants);
    }

    private static ShaderBinding Texture(string name, int slot, int components)
    {
        return new ShaderBinding(name, ShaderBindingKind.Texture, slot, components);
    }

    private static ShaderBinding Output(int components)
    {
        return new ShaderBinding("OutputTexture", ShaderBindingKind.UnorderedAccess, 0, components);
    }

    private static ShaderBinding Sampler()
    {
        return new ShaderBinding("Sampler", ShaderBindingKind.Sampler, 0, 1);
    }
}
