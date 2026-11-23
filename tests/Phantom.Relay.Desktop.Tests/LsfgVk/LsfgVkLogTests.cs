// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkLogTests
{
    // What lsfg-vk 2.0.0 wrote when it loaded inside Nightcast (seen in a run on Linux).
    private const string Started = """
        (lsfg-vk) [INFO]: Loaded lsfg-vk layer version 2.0.0
        (lsfg-vk) [INFO]: Using profile with name 'Nightcast' (identified via environment)
        (lsfg-vk) [INFO]:   Multiplier: 2
        """;

    [Fact]
    public void A_layer_that_started_with_nightcasts_profile_reports_its_version()
    {
        Assert.Equal("2.0.0", LsfgVkLog.LoadedVersion(Started));
    }

    [Fact]
    public void A_layer_that_loaded_without_the_profile_or_wrote_nothing_did_not_start()
    {
        Assert.Null(LsfgVkLog.LoadedVersion("(lsfg-vk) [INFO]: Loaded lsfg-vk layer version 2.0.0\n"));
        Assert.Null(LsfgVkLog.LoadedVersion(string.Empty));
    }

    [Fact]
    public void Only_what_the_log_gained_since_counts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lsfgvk-log-{Guid.NewGuid():N}.log");

        try
        {
            File.WriteAllText(path, Started);
            long before = LsfgVkLog.Length(path);
            File.AppendAllText(path, "\n(lsfg-vk) [ERROR]: no dll\n");

            Assert.Null(LsfgVkLog.LoadedVersion(LsfgVkLog.ReadFrom(path, before)));
            Assert.Equal("2.0.0", LsfgVkLog.LoadedVersion(LsfgVkLog.ReadFrom(path, 0)));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
