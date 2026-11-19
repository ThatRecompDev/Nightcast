// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Session;

/// <summary>
/// The console offering's REST paths and the fixed values of its payloads, as the public web player sends them
/// (docs/architecture/xhome.md#sessions).
/// </summary>
public static class XHomeProtocol
{
    public const string ConsolesPath = "v6/servers/home";

    public const string PlayPath = "v5/sessions/home/play";

    public const string StatePath = "state";

    public const string SdpPath = "sdp";

    public const string IcePath = "ice";

    public const string KeepAlivePath = "keepalive";

    /// <summary>The streaming stack the client asks for: WebRTC.</summary>
    public const string NanoVersion = "V3;WebrtcPath";

    public const string SdkType = "web";

    public const string OfferMessage = "offer";

    public const string IceMessage = "iceCandidate";

    public const string FirstRequestId = "1";

    public const string AnswerAccepted = "success";

    public const string EndOfCandidates = "end-of-candidates";

    /// <summary>The service writes candidates in their SDP attribute form; WebRTC stacks want them bare.</summary>
    public const string AttributePrefix = "a=";

    public const string ChatCodec = "opus";

    public const string ChatContainer = "webm";

    public const int ChatBytesPerSample = 2;

    public const int ChatClipMilliseconds = 20;

    public const int ChatChannels = 1;

    public const int ChatSampleRate = 24000;

    public const int ChatMaxVersion = 1;

    public const int ControlMaxVersion = 3;

    public const int InputMaxVersion = 8;

    public const int MessageMaxVersion = 1;

    public static readonly TimeSpan StatePollInterval = TimeSpan.FromSeconds(1);

    public static readonly TimeSpan ExchangePollInterval = TimeSpan.FromMilliseconds(500);

    public static readonly TimeSpan ProvisionTimeout = TimeSpan.FromMinutes(2);

    public static readonly TimeSpan ExchangeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>How often a running session is kept alive; the service ends a silent one.</summary>
    public static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);
}
