// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Architecture.Policy;
using Phantom.Engine.Architecture.Rules;

namespace Phantom.Relay.Architecture.Tests.Repository;

/// <summary>
/// Phantom.Relay's architecture: the header every code file carries and the layers of docs/architecture/overview.md.
/// A layer gains its row here before its first project lands. The first layer that holds a project is its layer, so
/// the tests come first.
/// </summary>
public static class RelayPolicy
{
    public const string Xbox = "Xbox";
    public const string Profiles = "Profiles";
    public const string Stream = "Stream";
    public const string App = "App";
    public const string Heads = "Heads";
    public const string Tests = "Tests";

    public static FileHeader Header { get; } = new(["Copyright (c) 2026 Phantom.Relay. All rights reserved."]);

    public static ArchitecturePolicy Policy { get; } = new(
        Header: Header,
        Layers:
        [
            new Layer(Tests, ["*.Tests"], [Xbox, Profiles, Stream, App, Heads]),
            new Layer(Xbox, ["Phantom.Relay.Xbox"], []),
            new Layer(Profiles, ["Phantom.Relay.Profiles"], [Xbox]),
            new Layer(Stream, ["Phantom.Relay.Stream"], [Profiles, Xbox]),
            new Layer(App, ["Phantom.Relay.App"], [Stream, Profiles, Xbox]),
            new Layer(Heads, ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter"], [App, Stream, Profiles, Xbox]),
        ],
        ExcludedPaths: [],
        AmbientStateProjects: ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "*.Tests"],
        MagicNumberExemptProjects: ["*.Tests"],
        HotPathValueTypes: ["XboxPadState", "RumbleReport"],
        GeneratorAttributes: ["JsonSerializable", "LoggerMessage", "GeneratedRegex", "JSImport", "JSExport", "LibraryImport"])
    {
        CompositionRootProjects = ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "*.Tests"],
    };
}
