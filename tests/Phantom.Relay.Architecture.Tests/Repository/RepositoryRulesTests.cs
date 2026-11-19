// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Architecture.Policy;
using Phantom.Engine.Architecture.Reporting;
using Phantom.Engine.Architecture.Rules;

namespace Phantom.Relay.Architecture.Tests.Repository;

/// <summary>Phantom.Relay follows every rule of Phantom.Engine's rulebook, one test case per rule.</summary>
public sealed class RepositoryRulesTests
{
    private static readonly Lazy<ArchitectureCheck> Check =
        new(static () => ArchitectureCheck.Load(RepositoryRoot.FindFrom(AppContext.BaseDirectory), RelayPolicy.Policy));

    public static TheoryData<string> Rules()
    {
        return [.. RuleNames.All];
    }

    [Theory]
    [MemberData(nameof(Rules))]
    public void Relay_follows_the_rule(string rule)
    {
        var violations = Check.Value.Run(rule);

        Assert.True(violations.Count == 0, ViolationReport.Format(violations));
    }

    [Fact]
    public void The_check_reads_the_repository_and_its_scripts()
    {
        var files = Check.Value.Files;

        Assert.Contains(files, static file => file.RelativePath == "tests/Phantom.Relay.Architecture.Tests/Repository/RelayPolicy.cs");
        Assert.Contains(files, static file => file.RelativePath == ".githooks/commit-msg");
        Assert.Contains(files, static file => file.RelativePath == "scripts/test/test-fast.ps1");
    }
}
