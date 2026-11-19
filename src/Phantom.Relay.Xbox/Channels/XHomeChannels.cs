// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Channels;

/// <summary>
/// The data channels the console expects, by label and subprotocol, and the fixed values of their messages
/// (docs/architecture/xhome.md#data-channels).
/// </summary>
public static class XHomeChannels
{
    public const string InputLabel = "input";

    public const string InputProtocol = "1.0";

    public const string ControlLabel = "control";

    public const string ControlProtocol = "controlV1";

    public const string MessageLabel = "message";

    public const string MessageProtocol = "messageV1";

    public const string ChatLabel = "chat";

    public const string ChatProtocol = "chatV1";

    /// <summary>The fixed key the web player sends in its authorization request: part of the protocol, not a secret.</summary>
    public const string ControlAccessKey = "4BDB3609-C1F1-4195-9B37-FEFF45DA8B8E";

    public const string AuthorizationRequest = "authorizationRequest";

    public const string GamepadChanged = "gamepadChanged";

    public const string KeyframeRequested = "videoKeyframeRequested";

    public const string HandshakeType = "Handshake";

    public const string HandshakeAckType = "HandshakeAck";

    public const string MessageType = "Message";

    public const string TransactionStartType = "TransactionStart";

    public const string TransactionCompleteType = "TransactionComplete";

    public const string FirstCorrelation = "0";

    public const string SystemUiTarget = "/streaming/systemUi/configuration";

    public const string InstallTarget = "/streaming/properties/clientappinstallidchanged";

    public const string OrientationTarget = "/streaming/characteristics/orientationchanged";

    public const string TouchTarget = "/streaming/characteristics/touchinputenabledchanged";

    public const string CapabilitiesTarget = "/streaming/characteristics/clientdevicecapabilities";

    public const string DimensionsTarget = "/streaming/characteristics/dimensionschanged";

    /// <summary>The console's message naming the running game (seen live; its content's shape is not seen yet).</summary>
    public const string TitleInfoTarget = "/streaming/properties/titleinfo";

    public const int SystemUiMajor = 0;

    public const int SystemUiMinor = 2;

    public const int SystemUiPatch = 0;

    public const int Landscape = 0;
}
