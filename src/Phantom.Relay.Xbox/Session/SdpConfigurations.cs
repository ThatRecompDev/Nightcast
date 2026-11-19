// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Session;

/// <summary>The channel versions and chat format this client offers.</summary>
public static class SdpConfigurations
{
    public static SdpConfiguration Client()
    {
        var chat = new ChatConfiguration(
            XHomeProtocol.ChatBytesPerSample,
            XHomeProtocol.ChatClipMilliseconds,
            new ChatFormat(XHomeProtocol.ChatCodec, XHomeProtocol.ChatContainer),
            XHomeProtocol.ChatChannels,
            XHomeProtocol.ChatSampleRate);

        return new SdpConfiguration(
            chat,
            new ChannelVersion(1, XHomeProtocol.ChatMaxVersion),
            new ChannelVersion(1, XHomeProtocol.ControlMaxVersion),
            new ChannelVersion(1, XHomeProtocol.InputMaxVersion),
            new ChannelVersion(1, XHomeProtocol.MessageMaxVersion));
    }
}
