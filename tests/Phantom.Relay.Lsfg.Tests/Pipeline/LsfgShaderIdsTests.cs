// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Pipeline;

namespace Phantom.Relay.Lsfg.Tests.Pipeline;

public sealed class LsfgShaderIdsTests
{
    [Theory]
    [InlineData(LsfgShaderIds.Mipmaps, false, 255)]
    [InlineData(LsfgShaderIds.Mipmaps, true, 255)]
    [InlineData(LsfgShaderIds.Generate, true, 256)]
    [InlineData(LsfgShaderIds.Correlation, false, 257)]
    [InlineData(LsfgShaderIds.Correlation, true, 280)]
    [InlineData(LsfgShaderIds.BetaFirst + LsfgShaderIds.BetaSteps - 1, true, 302)]
    public void A_role_resolves_to_its_id_in_each_set(int role, bool performance, int id)
    {
        Assert.Equal(id, LsfgShaderIds.Of(role, performance));
    }

    [Fact]
    public void The_roles_cover_257_to_279_once_each()
    {
        int[] firsts = [LsfgShaderIds.Correlation, LsfgShaderIds.BlendCorrelation];
        var roles = new List<int>(firsts);
        roles.AddRange(Enumerable.Range(LsfgShaderIds.GammaFirst, LsfgShaderIds.GammaSteps));
        roles.AddRange(Enumerable.Range(LsfgShaderIds.DeltaFlowFirst, LsfgShaderIds.DeltaFlowSteps));
        roles.AddRange(Enumerable.Range(LsfgShaderIds.AlphaFirst, LsfgShaderIds.AlphaSteps));
        roles.AddRange(Enumerable.Range(LsfgShaderIds.BlendFirst, LsfgShaderIds.BlendSteps));

        roles.AddRange(Enumerable.Range(LsfgShaderIds.BetaFirst, LsfgShaderIds.BetaSteps));

        Assert.Equal(Enumerable.Range(257, 23), roles.Order());
    }
}
