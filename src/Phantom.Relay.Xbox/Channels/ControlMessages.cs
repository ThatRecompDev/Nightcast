// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Relay.Xbox.Channels.Payloads;
using Phantom.Relay.Xbox.Json;

namespace Phantom.Relay.Xbox.Channels;

/// <summary>The control channel's messages. On open the client sends the authorization, then pad 0 as added.</summary>
public static class ControlMessages
{
    public static string Authorization()
    {
        var message = new ControlAuthorization(XHomeChannels.AuthorizationRequest, XHomeChannels.ControlAccessKey);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.ControlAuthorization);
    }

    public static string GamepadChanged(int index, bool added)
    {
        var message = new ControlGamepadChanged(XHomeChannels.GamepadChanged, index, added);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.ControlGamepadChanged);
    }

    public static string KeyframeRequest()
    {
        var message = new ControlKeyframeRequest(XHomeChannels.KeyframeRequested, IfrRequested: true);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.ControlKeyframeRequest);
    }
}
