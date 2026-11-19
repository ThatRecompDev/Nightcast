// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Pipeline;
using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg.Tests.Pipeline;

public sealed class LsfgConstantLayoutTests
{
    [Fact]
    public void The_constants_struct_is_the_buffer_size()
    {
        Assert.Equal(LsfgConstantLayout.Size, System.Runtime.InteropServices.Marshal.SizeOf<LsfgConstants>());
    }

    [Fact]
    public void A_buffer_with_the_reflected_offsets_is_accepted()
    {
        var layout = Layout(32, ("InputOffset", 0), ("FirstIter", 8), ("FirstIterS", 12), ("AdvancedColorKind", 16), ("HdrSupport", 20), ("ResolutionInvScale", 24), ("Timestamp", 28));

        Assert.Null(LsfgConstantLayout.Check(layout));
    }

    [Fact]
    public void A_moved_field_is_refused_with_its_shader_and_offsets()
    {
        var layout = Layout(32, ("Timestamp", 24));

        Assert.Equal("shader 300 has constant 'Timestamp' at byte 24; the pipeline expects 28", LsfgConstantLayout.Check(layout));
    }

    [Fact]
    public void An_unknown_field_or_a_larger_buffer_is_refused()
    {
        Assert.NotNull(LsfgConstantLayout.Check(Layout(32, ("Sharpness", 0))));
        Assert.NotNull(LsfgConstantLayout.Check(Layout(64)));
    }

    private static ShaderLayout Layout(int size, params (string Name, int Offset)[] fields)
    {
        var constants = fields.Select(static field => new ShaderConstant(field.Name, field.Offset, 4, Used: true)).ToList();

        return new ShaderLayout(300, 8, 8, [], size, constants);
    }
}
