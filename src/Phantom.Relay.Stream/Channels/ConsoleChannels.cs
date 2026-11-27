// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Tls;
using Phantom.Engine.Core.Randomness;
using Phantom.Relay.Stream.Input;
using Phantom.Relay.Stream.Link;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.WebRtc.Sctp;
using Phantom.Relay.Xbox.Channels;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.Stream.Channels;

/// <summary>
/// The console's data channels over the DTLS session: SCTP as the DTLS client, the four channels opened, the console's
/// opening exchange spoken on each as it opens (docs/architecture/xhome.md#data-channels), the running title read from
/// the message channel, and every pad state waiting in the <see cref="PadMailbox"/> written onto the input channel the
/// moment the pump sees it. Pumped from the network loop's thread, like everything after the handshake.
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
    private readonly InputReportWriter _reports;
    private readonly PadMailbox _pads;
    private readonly RumbleMailbox _rumble;
    private readonly IRandomSource _random;
    private readonly PadAnnouncement _firstPad;
    private readonly byte[] _record;
    private readonly byte[] _report = new byte[InputReportWriter.GamepadReportSize];
    private readonly bool[] _announced = new bool[PadMailbox.MaxPads];
    private readonly List<DataChannel> _channels = [];
    private readonly HashSet<ushort> _greeted = [];
    private readonly List<string> _opened = [];
    private readonly List<string> _kinds = [];

    private DataChannel? _input;
    private DataChannel? _control;
    private bool _established;

    public ConsoleChannels(DtlsTransport dtls, DtlsDatagramBridge bridge, PadMailbox pads, RumbleMailbox rumble, TimeProvider clock, IRandomSource random)
    {
        _dtls = dtls;
        _bridge = bridge;
        _pads = pads;
        _rumble = rumble;
        _random = random;
        _firstPad = new PadAnnouncement(clock);

        // SCTP over the session, the channels on it, the input reports and the room for one record.
        _association = new SctpAssociation(new DtlsSctpOutput(dtls), clock, random, SctpOptions.Default);
        _hub = new DataChannelHub(_association, DataChannelSide.DtlsClient);
        _reports = new InputReportWriter(clock);
        _record = new byte[dtls.GetReceiveLimit()];
    }

    public int Received { get; private set; }

    public int Sent { get; private set; }

    /// <summary>Gamepad reports written onto the input channel.</summary>
    public long InputReportsSent { get; private set; }

    /// <summary>The running game's title id, once the console named it.</summary>
    public string? TitleId { get; private set; }

    /// <summary>True once the association was established and has closed again: the console ended the stream.</summary>
    public bool HasEnded => _established && _association.State == SctpAssociationState.Closed;

    /// <summary>
    /// How the association ended, for the failure message: the close reason, the ABORT's cause code when the console
    /// gave one (RFC 9260 section 3.3.10: 12 user-initiated, 13 protocol violation), and the data timeouts in a row.
    /// </summary>
    public string EndReason => string.Create(
        CultureInfo.InvariantCulture,
        $"SCTP {_association.CloseReason}, abort cause {_association.PeerAbortCause}, {_association.TimeoutsInARow} data timeouts in a row");

    /// <summary>True when the console acknowledged everything sent on the channels (after <see cref="Leave"/>).</summary>
    public bool Delivered => _association.AllAcknowledged;

    /// <summary>
    /// Before the session ends: every pad announced is removed on the control channel, as when a pad is unplugged, so
    /// the console does not keep a phantom controller signed in for the next stream to queue behind. False when the
    /// control channel is not open (the console already closed it): nothing to say.
    /// </summary>
    public bool Leave()
    {
        var control = _control;

        if (control is null || control.State != DataChannelState.Open || _association.State != SctpAssociationState.Established)
        {
            return false;
        }

        for (int index = 0; index < _announced.Length; index++)
        {
            if (_announced[index])
            {
                SendText(control, ControlMessages.GamepadChanged(index, false));
                _announced[index] = false;
            }
        }

        _association.Service();

        return true;
    }

    /// <summary>Starts the association; the channels are asked for once it is established.</summary>
    public void Open()
    {
        _association.Connect();
    }

    /// <summary>
    /// Decrypts what arrived, runs SCTP, greets channels that opened, answers the console's messages and sends the pads'
    /// waiting states.
    /// </summary>
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
        AnnounceFirstPad();
        SendPads();
    }

    public ChannelReport Report(TimeSpan? firstRtp)
    {
        return new ChannelReport(_association.State.ToString(), _opened, Received, Sent, _kinds, firstRtp);
    }

    private void RequestChannels()
    {
        if (_channels.Count > 0 || _association.State != SctpAssociationState.Established)
        {
            return;
        }

        _established = true;
        _input = _hub.Open(DataChannelOptions.Reliable(XHomeChannels.InputLabel, XHomeChannels.InputProtocol));
        _control = _hub.Open(DataChannelOptions.Reliable(XHomeChannels.ControlLabel, XHomeChannels.ControlProtocol));
        _channels.Add(_input);
        _channels.Add(_control);

        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.MessageLabel, XHomeChannels.MessageProtocol)));
        _channels.Add(_hub.Open(DataChannelOptions.Reliable(XHomeChannels.ChatLabel, XHomeChannels.ChatProtocol)));
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

    /// <summary>
    /// What each channel's opening needs: the control authorization, the message handshake, the input metadata. The
    /// first pad waits for the session to be fully up (<see cref="PadAnnouncement"/>).
    /// </summary>
    private void Greet(DataChannel channel)
    {
        switch (channel.Label)
        {
            case XHomeChannels.ControlLabel:
                SendText(channel, ControlMessages.Authorization());

                return;
            case XHomeChannels.MessageLabel:
                SendText(channel, MessageChannel.Handshake(MessageIds.Next(_random)));

                return;
            case XHomeChannels.InputLabel:
                int written = _reports.WriteClientMetadata(_report, 0);
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

            // The console's vibration comes as binary reports on the input channel, read before the payload is released.
            if (!isString && channel.Label == XHomeChannels.InputLabel && RumbleReportReader.TryRead(payload, out var rumble))
            {
                _hub.Release();
                _rumble.Post(in rumble);

                continue;
            }

            // The console's answer to the first pad: it took it.
            if (!isString && channel.Label == XHomeChannels.InputLabel && ServerMetadataReport.Is(payload))
            {
                _firstPad.Answered();
            }

            string text = Encoding.UTF8.GetString(payload);
            Note(channel.Label, isString, text);
            _hub.Release();

            if (channel.Label == XHomeChannels.MessageLabel)
            {
                AnswerMessage(channel, text);
            }
        }
    }

    private void AnswerMessage(DataChannel channel, string text)
    {
        TitleId = TitleInfo.Read(text) ?? TitleId;

        if (MessageChannel.IsTransactionComplete(text))
        {
            _firstPad.Unconstrained();
        }

        var client = new ClientDescription(DisplayWidth, DisplayHeight, MessageIds.Next(_random), MessageIds.Next(_random, ClientDescription.MessageCount));

        var replies = MessageChannel.Answer(text, client);

        if (replies.Count > 0)
        {
            _firstPad.Described();
        }

        foreach (string reply in replies)
        {
            SendText(channel, reply);
        }
    }

    /// <summary>The first pad, announced (or announced again) when <see cref="PadAnnouncement"/> says so.</summary>
    private void AnnounceFirstPad()
    {
        var control = _control;

        if (control is null || control.State != DataChannelState.Open)
        {
            return;
        }

        var step = _firstPad.Next();

        if (step == PadStep.None)
        {
            return;
        }

        if (step == PadStep.Reannounce)
        {
            SendText(control, ControlMessages.GamepadChanged(0, false));
        }

        SendText(control, ControlMessages.GamepadChanged(0, true));
        _announced[0] = true;
    }

    /// <summary>
    /// Every pad state waiting goes out at once, once the input channel is open and the first pad was announced; a
    /// state the association cannot take now goes back to wait for the next turn, unless a newer one came.
    /// </summary>
    private void SendPads()
    {
        var input = _input;

        if (input is null || input.State != DataChannelState.Open || !_firstPad.ReportsMayFlow)
        {
            return;
        }

        while (_pads.TryTake(out byte index, out var pad))
        {
            Announce(index);
            int written = _reports.WriteGamepad(_report, index, in pad);

            if (!_hub.SendBinary(input, _report.AsSpan(0, written)))
            {
                _pads.Restore(index, in pad);

                return;
            }

            InputReportsSent++;
            Sent++;
        }
    }

    /// <summary>A pad other than the first is announced on the control channel before its first report.</summary>
    private void Announce(byte index)
    {
        var control = _control;

        if (_announced[index] || control is null || control.State != DataChannelState.Open)
        {
            return;
        }

        SendText(control, ControlMessages.GamepadChanged(index, true));
        _announced[index] = true;
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
