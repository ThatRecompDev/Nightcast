// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.LsfgVk;

/// <summary>
/// The marker a start leaves while it loads lsfg-vk: set before the layer loads, cleared once a stream ran with it
/// (<see cref="LsfgVkFrameGeneration.Confirm"/>). Found at the next start, the last one crashed with the layer loaded.
/// A folder that cannot be written simply has no guard.
/// </summary>
public static class LsfgVkStartGuard
{
    public static bool Pending(string path)
    {
        return File.Exists(path);
    }

    public static void Set(string path)
    {
        try
        {
            File.WriteAllText(path, string.Empty);
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            // No guard: the start goes on.
        }
    }

    public static void Clear(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception unwritable) when (unwritable is IOException or UnauthorizedAccessException)
        {
            // Left: at worst the next start leaves the layer out once.
        }
    }
}
