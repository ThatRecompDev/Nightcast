// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows.Tests.Recording;

/// <summary>
/// Where the recording's live tests write their files: the folder <c>RELAY_RECORDER_TEST_DIR</c> names, else one under
/// the system's temporary folder. Never the repository.
/// </summary>
public static class RecordingTestFolder
{
    public const string Variable = "RELAY_RECORDER_TEST_DIR";

    /// <summary>A file path in a fresh folder named <paramref name="test"/> for one test's output.</summary>
    public static string File(string test, string name)
    {
        var root = Environment.GetEnvironmentVariable(Variable);

        if (string.IsNullOrEmpty(root))
        {
            root = Path.Combine(Path.GetTempPath(), "Phantom.Relay.Tests");
        }

        var folder = Path.Combine(root, test);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, name);

        if (System.IO.File.Exists(path))
        {
            System.IO.File.Delete(path);
        }

        return path;
    }
}
