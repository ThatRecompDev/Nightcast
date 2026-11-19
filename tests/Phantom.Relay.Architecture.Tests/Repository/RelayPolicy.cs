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
    public const string WebRtc = "WebRtc";
    public const string Xbox = "Xbox";
    public const string Profiles = "Profiles";
    public const string Adapters = "Adapters";
    public const string Stream = "Stream";
    public const string App = "App";
    public const string Heads = "Heads";
    public const string Tools = "Tools";
    public const string Tests = "Tests";

    public static FileHeader Header { get; } = new(["Copyright (c) 2026 Phantom.Relay. All rights reserved."]);

    public static ArchitecturePolicy Policy { get; } = new(
        Header: Header,
        Layers:
        [
            new Layer(Tests, ["*.Tests"], [WebRtc, Xbox, Profiles, Adapters, Stream, App, Heads, Tools]),
            new Layer(WebRtc, ["Phantom.Relay.WebRtc"], []),
            new Layer(Xbox, ["Phantom.Relay.Xbox"], []),
            new Layer(Profiles, ["Phantom.Relay.Profiles"], [Xbox]),
            new Layer(Adapters, ["Phantom.Relay.Http.*"], [Xbox]),
            new Layer(Stream, ["Phantom.Relay.Stream"], [WebRtc, Profiles, Xbox]),
            new Layer(App, ["Phantom.Relay.App"], [Stream, Profiles, Xbox]),
            new Layer(Heads, ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter"], [App, Stream, Adapters, WebRtc, Profiles, Xbox]),
            new Layer(Tools, ["Phantom.Relay.Tools.*"], [Adapters, Profiles, Xbox]),
        ],
        ExcludedPaths: [],
        AmbientStateProjects: ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "Phantom.Relay.Tools.*", "*.Tests"],
        MagicNumberExemptProjects: ["*.Tests"],
        HotPathValueTypes: ["XboxPadState", "RumbleReport", "DatagramEndpoint"],
        GeneratorAttributes: ["JsonSerializable", "LoggerMessage", "GeneratedRegex", "JSImport", "JSExport", "LibraryImport"])
    {
        CompositionRootProjects = ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "Phantom.Relay.Tools.*", "*.Tests"],
    };
}
