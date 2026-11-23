// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Setup;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Desktop;

/// <summary>
/// One run of the desktop as its switches and the user's store set it up: the service, the profiles and the stream's
/// settings, the setup and where its lines show (the window's screen and the text console), and the window's start.
/// </summary>
internal sealed record StreamRun(
    IXHomeSessionService Sessions,
    IReadOnlyList<StreamProfile> Profiles,
    StreamSettings Settings,
    BitrateFeedback Feedback,
    bool Fullscreen,
    ISetupFlow Setup,
    ScreenWriter Screen,
    ScreenConsoleChoice Choice,
    TimeProvider Clock);
