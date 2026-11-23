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
    public const string Lsfg = "Lsfg";
    public const string Nvidia = "Nvidia";
    public const string Audio = "Audio";
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
            new Layer(Tests, ["*.Tests"], [WebRtc, Xbox, Profiles, Adapters, Lsfg, Nvidia, Audio, Stream, App, Heads, Tools]),
            new Layer(WebRtc, ["Phantom.Relay.WebRtc"], []),
            new Layer(Xbox, ["Phantom.Relay.Xbox"], []),
            new Layer(Audio, ["Phantom.Relay.Audio"], []),
            new Layer(Profiles, ["Phantom.Relay.Profiles"], [Xbox]),
            new Layer(Adapters, ["Phantom.Relay.Http.*", "Phantom.Relay.Pads.*", "Phantom.Relay.Media.*"], [Xbox]),
            // Optional frame generation on the decoder's D3D11 device, sharing Media.Windows' COM definitions.
            new Layer(Lsfg, ["Phantom.Relay.Lsfg"], [Adapters]),
            // NVIDIA FRUC, the second optional frame generator: behind LSFG's seam (IFrameGenerationStage), on its device
            // wrapper, colour conversions and timer, so it sits above LSFG rather than beside it (docs/architecture/nvidia-fruc.md).
            new Layer(Nvidia, ["Phantom.Relay.Nvidia"], [Lsfg, Adapters]),
            new Layer(Stream, ["Phantom.Relay.Stream"], [Audio, WebRtc, Profiles, Xbox]),
            new Layer(App, ["Phantom.Relay.App"], [Stream, Audio, WebRtc, Profiles, Xbox]),
            // The desktop head runs the optional frame generation on its decoder's GPU path (docs/architecture/lsfg.md).
            new Layer(Heads, ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter"], [App, Stream, Nvidia, Lsfg, Adapters, Audio, WebRtc, Profiles, Xbox]),
            new Layer(Tools, ["Phantom.Relay.Tools.*"], [Stream, Adapters, Audio, WebRtc, Profiles, Xbox]),
        ],
        ExcludedPaths: [],
        AmbientStateProjects: ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "Phantom.Relay.Tools.*", "*.Tests"],
        MagicNumberExemptProjects: ["*.Tests"],
        HotPathValueTypes: ["XboxPadState", "RumbleReport", "DatagramEndpoint", "PacedFrame", "GpuPicture", "GpuSlotRelease", "ShownFrame", "GamepadState", "InputEvent"],
        GeneratorAttributes: ["JsonSerializable", "LoggerMessage", "GeneratedRegex", "JSImport", "JSExport", "LibraryImport", "GeneratedComInterface"])
    {
        CompositionRootProjects = ["Phantom.Relay.Desktop", "Phantom.Relay.Browser", "Phantom.Relay.Specter", "Phantom.Relay.Tools.*", "*.Tests"],
    };
}
