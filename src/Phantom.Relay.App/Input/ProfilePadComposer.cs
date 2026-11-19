// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;
using Phantom.Relay.Profiles;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>The app's pad: the profile's <see cref="PadComposer"/> (the pad by place, its shaping, keyboard and mouse).</summary>
public sealed class ProfilePadComposer : IPadComposer
{
    private PadComposer _composer;

    public ProfilePadComposer(StreamProfile profile)
    {
        _composer = new PadComposer(profile);
    }

    [HotPath]
    public XboxPadState Compose(InputFrame input, float elapsedSeconds)
    {
        return _composer.Compose(input, elapsedSeconds);
    }

    public void Use(StreamProfile profile)
    {
        _composer = new PadComposer(profile);
    }
}
