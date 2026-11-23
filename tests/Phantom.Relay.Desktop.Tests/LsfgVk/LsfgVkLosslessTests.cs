// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Desktop.LsfgVk;

namespace Phantom.Relay.Desktop.Tests.LsfgVk;

public sealed class LsfgVkLosslessTests
{
    [Fact]
    public void The_lsfg_vk_file_of_the_beta_branch_is_found_in_any_library()
    {
        using var folder = new TempFolder();
        var file = Lossless(Path.Combine(folder.Path, "second"), "lsfg-vk.dll");

        var found = LsfgVkLossless.Find([Path.Combine(folder.Path, "first"), Path.Combine(folder.Path, "second")]);

        Assert.Equal(new LsfgVkLossless(file, null), found);
    }

    [Fact]
    public void Lossless_Scaling_without_its_lsfg_vk_branch_says_how_to_choose_it()
    {
        using var folder = new TempFolder();
        Lossless(folder.Path, "Lossless.dll");

        var found = LsfgVkLossless.Find([folder.Path]);

        Assert.Null(found.Path);
        Assert.Contains("Betas, lsfg-vk", found.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_Lossless_Scaling_it_says_so()
    {
        using var folder = new TempFolder();

        var found = LsfgVkLossless.Find([folder.Path]);

        Assert.Null(found.Path);
        Assert.Contains("not found in Steam", found.Problem, StringComparison.Ordinal);
    }

    private static string Lossless(string library, string name)
    {
        var path = Path.Combine(library, "steamapps", "common", "Lossless Scaling", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "dll");

        return path;
    }
}
