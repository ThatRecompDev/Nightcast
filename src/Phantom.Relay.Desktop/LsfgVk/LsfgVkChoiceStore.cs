// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Engine.Content.Storage;
using Phantom.Relay.Profiles;

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The player's last frame-generation choice for lsfg-vk, in the user's store (one key, the profiles' JSON): whether it
/// was on decides whether the next start loads the layer, and its setting and multiplier come back with it.
/// </summary>
public sealed class LsfgVkChoiceStore
{
    public const string Key = "lsfgvk.choice";

    private readonly IPersistentStore _store;

    public LsfgVkChoiceStore(IPersistentStore store)
    {
        _store = store;
    }

    /// <summary>The choice kept; off when none was, or what was kept cannot be read.</summary>
    public FrameGeneration Load()
    {
        var bytes = _store.Read(Key);

        if (bytes is null)
        {
            return FrameGeneration.Off;
        }

        try
        {
            return JsonSerializer.Deserialize(bytes, ProfilesJsonContext.Default.FrameGeneration) ?? FrameGeneration.Off;
        }
        catch (JsonException)
        {
            return FrameGeneration.Off;
        }
    }

    /// <summary>Keeps <paramref name="settings"/>; a store that refuses keeps the last one.</summary>
    public void Save(FrameGeneration settings)
    {
        _ = _store.Write(Key, JsonSerializer.SerializeToUtf8Bytes(settings, ProfilesJsonContext.Default.FrameGeneration));
    }
}
