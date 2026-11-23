// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Engine.Content.Storage;
using Phantom.Relay.Desktop.LsfgVk;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkChoiceStoreTests
{
    [Fact]
    public void The_choice_is_kept_between_runs_and_none_kept_is_off()
    {
        var memory = new MemoryPersistentStore();
        var chosen = FrameGeneration.QualityDoubled with { Multiplier = 3, FlowScale = 0.5f, Mode = FrameGenerationMode.Performance };

        Assert.Same(FrameGeneration.Off, new LsfgVkChoiceStore(memory).Load());
        new LsfgVkChoiceStore(memory).Save(chosen);

        Assert.Equal(chosen, new LsfgVkChoiceStore(memory).Load());
    }

    [Fact]
    public void A_kept_choice_that_cannot_be_read_is_off()
    {
        var memory = new MemoryPersistentStore();
        _ = memory.Write(LsfgVkChoiceStore.Key, Encoding.UTF8.GetBytes("{ not json"));

        Assert.Same(FrameGeneration.Off, new LsfgVkChoiceStore(memory).Load());
    }
}
