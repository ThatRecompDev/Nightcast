// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Tls;
using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Sctp;
using Phantom.Relay.Xbox.Channels;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Stream.Channels;

/// <summary>
/// The console's data channels over the DTLS session: SCTP as the DTLS client, the four channels opened, and the
/// console's opening exchange spoken on each as it opens (docs/architecture/xhome.md#data-channels). Pumped from the
/// network loop's thread, like everything after the handshake.
/// </summary>
public sealed class ConsoleChannels
{
    private const int DisplayWidth = 2560;
    private const int DisplayHeight = 1440;
    private const int KindsKept = 12;
    private const int ReadWaitMillis = 1;

    private readonly DtlsTransport _dtls;
    private readonly DtlsDatagramBridge _bridge;
    private readonly SctpAssociation _association;
    private readonly DataChannelHub _hub;
    private readonly InputReportWriter _input;
    private readonly IRandomSource _random;
    private readonly byte[] _record;
    private readonly byte[] _report = new byte[InputReportWriter.GamepadReportSize];
    private readonly List<DataChannel> _channels = [];
    private readonly HashSet<ushort> _greeted = [];
    private readonly List<string> _opened = [];
    private readonly List<string> _kinds = [];

    public ConsoleChannels(DtlsTransport dtls, DtlsDatagramBridge bridge, TimeProvider clock, IRandomSource random)
    {
        _dtls = dtls;
        _bridge = bridge;
        _random = random;

        // SCTP over the session, the channels on it, the input reports and the room for one record.
        _association = new SctpAssociation(new DtlsSctpOutput(dtls), clock, random, SctpOptions.Default);
        _hub = new DataChannelHub(_association, DataChannelSide.DtlsClient);
        _input = new InputReportWriter(clock);
        _record = new byte[dtls.GetReceiveLimit()];
    }

    public int Received { get; private set; }

    public int Sent { get; private set; }

    /// <summary>Starts the association; the channels are asked for once it is established.</summary>
    public void Open()
    {
        _association.Connect();
    }

    private void RequestChannels()
    {
        if (_channels.Count > 0 || _association.State != SctpAssociationState.Established)
        {
            return;
        }

        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.InputLabel, XHomeChannels.InputProtocol)));
        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.ControlLabel, XHomeChannels.ControlProtocol)));
        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.MessageLabel, XHomeChannels.MessageProtocol)));
        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.ChatLabel, XHomeChannels.ChatProtocol)));
    }

    /// <summary>Decrypts what arrived, runs SCTP, greets channels that opened and answers the console's messages.</summary>
    public void Pump()
    {
        // Only when a record waits, and then with a 1 ms bound: BouncyCastle reads a wait of 0 as "forever".
        while (_bridge.HasIncoming)
        {
            int length = _dtls.Receive(_record, 0, _record.Length, ReadWaitMillis);

            if (length <= 0)
            {
                break;
            }

            _association.Receive(_record.AsSpan(0, length));
        }

        _association.Service();
        RequestChannels();
        GreetOpened();
        Answer();
    }

    public ChannelReport Report(TimeSpan? firstRtp)
    {
        return new ChannelReport(_association.State.ToString(), _opened, Received, Sent, _kinds, firstRtp);
    }

    private void GreetOpened()
    {
        foreach (var channel in _channels)
        {
            if (channel.State != DataChannelState.Open || !_greeted.Add(channel.Id))
            {
                continue;
            }

            _opened.Add(channel.Label);
            Greet(channel);
        }
    }

    /// <summary>What each channel's opening needs: the control authorization, the message handshake, the input metadata.</summary>
    private void Greet(DataChannel channel)
    {
        switch (channel.Label)
        {
            case XHomeChannels.ControlLabel:
                SendText(channel, ControlMessages.Authorization());
                SendText(channel, ControlMessages.GamepadChanged(0, true));

                return;
            case XHomeChannels.MessageLabel:
                SendText(channel, MessageChannel.Handshake(MessageIds.Next(_random)));

                return;
            case XHomeChannels.InputLabel:
                int written = _input.WriteClientMetadata(_report, 0);
                _hub.SendBinary(channel, _report.AsSpan(0, written));
                Sent++;

                return;
            default:
                return;
        }
    }

    private void Answer()
    {
        while (_hub.TryReceive(out var channel, out bool isString, out var payload))
        {
            Received++;
            string text = Encoding.UTF8.GetString(payload);
            Note(channel.Label, isString, text);
            _hub.Release();

            if (channel.Label == XHomeChannels.MessageLabel)
            {
                var client = new ClientDescription(DisplayWidth, DisplayHeight, MessageIds.Next(_random), MessageIds.Next(_random, ClientDescription.MessageCount));

                foreach (string reply in MessageChannel.Answer(text, client))
                {
                    SendText(channel, reply);
                }
            }
        }
    }

    /// <summary>Keeps the kind of a console message (its type and target, or "binary"), never its contents.</summary>
    private void Note(string label, bool isString, string text)
    {
        if (_kinds.Count >= KindsKept)
        {
            return;
        }

        if (!isString)
        {
            _kinds.Add($"{label}: binary");

            return;
        }

        _kinds.Add($"{label}: {Kind(text)}");
    }

    private static string Kind(string text)
    {
        try
        {
            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            string type = Field(root, "type") ?? Field(root, "message") ?? "?";
            string? target = Field(root, "target");

            if (target is null)
            {
                return type;
            }

            return $"{type} {target}";
        }
        catch (JsonException)
        {
            return "not JSON";
        }
    }

    private static string? Field(JsonElement root, string name)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private void SendText(DataChannel channel, string text)
    {
        _hub.SendString(channel, Encoding.UTF8.GetBytes(text));
        Sent++;
    }
}
