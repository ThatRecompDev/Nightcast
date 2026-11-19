// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json.Serialization;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Auth.Payloads;
using Phantom.Relay.Xbox.Channels.Payloads;
using Phantom.Relay.Xbox.Session.Payloads;

namespace Phantom.Relay.Xbox.Json;

/// <summary>Source-generated JSON metadata for every payload Relay reads or writes (NativeAOT- and trim-safe).</summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(MsaTokenReply))]
[JsonSerializable(typeof(UserTokenRequest))]
[JsonSerializable(typeof(XstsRequest))]
[JsonSerializable(typeof(XblTokenReply))]
[JsonSerializable(typeof(XstsErrorReply))]
[JsonSerializable(typeof(StreamingLoginRequest))]
[JsonSerializable(typeof(StreamingLoginReply))]
[JsonSerializable(typeof(StoredCredentials))]
[JsonSerializable(typeof(ConsoleListReply))]
[JsonSerializable(typeof(PlayRequest))]
[JsonSerializable(typeof(PlayReply))]
[JsonSerializable(typeof(SessionStateReply))]
[JsonSerializable(typeof(SdpOfferRequest))]
[JsonSerializable(typeof(ExchangeReply))]
[JsonSerializable(typeof(SdpAnswer))]
[JsonSerializable(typeof(IceRequest))]
[JsonSerializable(typeof(IReadOnlyList<IceCandidatePayload>))]
[JsonSerializable(typeof(ControlAuthorization))]
[JsonSerializable(typeof(ControlGamepadChanged))]
[JsonSerializable(typeof(ControlKeyframeRequest))]
[JsonSerializable(typeof(MessageHandshake))]
[JsonSerializable(typeof(MessageEnvelope))]
[JsonSerializable(typeof(SystemUiConfiguration))]
[JsonSerializable(typeof(ClientInstallChanged))]
[JsonSerializable(typeof(OrientationChanged))]
[JsonSerializable(typeof(TouchInputChanged))]
[JsonSerializable(typeof(DeviceCapabilities))]
[JsonSerializable(typeof(DimensionsChanged))]
public sealed partial class RelayJsonContext : JsonSerializerContext
{
}
