// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Relay.Xbox.Channels.Payloads;
using Phantom.Relay.Xbox.Json;

namespace Phantom.Relay.Xbox.Channels;

/// <summary>
/// The message channel as a pure exchange: the client opens with a handshake; the console's acknowledgement is
/// answered with the client's description; each transaction the console starts is completed. Ids come from the
/// caller, so a test pins every byte.
/// </summary>
public static class MessageChannel
{
    public static string Handshake(string id)
    {
        var message = new MessageHandshake(XHomeChannels.HandshakeType, XHomeChannels.MessageProtocol, id, XHomeChannels.FirstCorrelation);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.MessageHandshake);
    }

    /// <summary>
    /// The replies to one message from the console: the client's description after the handshake's acknowledgement,
    /// followed by the un-constrain transaction; a completion for a transaction start; nothing for anything else
    /// (unknown messages are ignored).
    /// </summary>
    public static IReadOnlyList<string> Answer(string received, ClientDescription client)
    {
        MessageEnvelope? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize(received, RelayJsonContext.Default.MessageEnvelope);
        }
        catch (JsonException)
        {
            return [];
        }

        if (envelope is null)
        {
            return [];
        }

        if (envelope.Type == XHomeChannels.HandshakeAckType)
        {
            return Describe(client);
        }

        if (envelope.Type == XHomeChannels.TransactionStartType && envelope.Id is not null)
        {
            return [Complete(envelope.Id)];
        }

        return [];
    }

    /// <summary>
    /// Whether a message from the console completes a transaction the client started: the only one is the
    /// un-constrain, so its completion means the game now takes input.
    /// </summary>
    public static bool IsTransactionComplete(string received)
    {
        try
        {
            return JsonSerializer.Deserialize(received, RelayJsonContext.Default.MessageEnvelope)?.Type == XHomeChannels.TransactionCompleteType;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string[] Describe(ClientDescription client)
    {
        var dimensions = new DimensionsChanged(
            client.Width,
            client.Height,
            client.Width,
            client.Height,
            0,
            0,
            client.Width,
            client.Height,
            SupportsCustomResolution: true);
        int[] version = [XHomeChannels.SystemUiMajor, XHomeChannels.SystemUiMinor, XHomeChannels.SystemUiPatch];

        // The targets and their contents, in the order the web player sends them.
        string[] targets =
        [
            XHomeChannels.SystemUiTarget,
            XHomeChannels.InstallTarget,
            XHomeChannels.OrientationTarget,
            XHomeChannels.TouchTarget,
            XHomeChannels.CapabilitiesTarget,
            XHomeChannels.DimensionsTarget,
        ];
        string[] contents =
        [
            JsonSerializer.Serialize(new SystemUiConfiguration(version, []), RelayJsonContext.Default.SystemUiConfiguration),
            JsonSerializer.Serialize(new ClientInstallChanged(client.InstallId), RelayJsonContext.Default.ClientInstallChanged),
            JsonSerializer.Serialize(new OrientationChanged(XHomeChannels.Landscape), RelayJsonContext.Default.OrientationChanged),
            JsonSerializer.Serialize(new TouchInputChanged(TouchInputEnabled: false), RelayJsonContext.Default.TouchInputChanged),
            JsonSerializer.Serialize(new DeviceCapabilities(), RelayJsonContext.Default.DeviceCapabilities),
            JsonSerializer.Serialize(dimensions, RelayJsonContext.Default.DimensionsChanged),
        ];

        var messages = new string[targets.Length + 1];

        for (int i = 0; i < targets.Length; i++)
        {
            messages[i] = Message(targets[i], client.Ids[i], contents[i]);
        }

        // Last, as the web player sends it: the game may take input. The console answers with TransactionComplete.
        string constrain = JsonSerializer.Serialize(new TitleConstrain(Constrain: false), RelayJsonContext.Default.TitleConstrain);
        messages[targets.Length] = Start(XHomeChannels.ConstrainTarget, client.Ids[targets.Length], constrain);

        return messages;
    }

    private static string Complete(string id)
    {
        var message = new MessageEnvelope(XHomeChannels.TransactionCompleteType, string.Empty, id, null, string.Empty);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.MessageEnvelope);
    }

    private static string Start(string target, string id, string content)
    {
        var message = new MessageEnvelope(XHomeChannels.TransactionStartType, content, id, target, string.Empty);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.MessageEnvelope);
    }

    private static string Message(string target, string id, string content)
    {
        var message = new MessageEnvelope(XHomeChannels.MessageType, content, id, target, string.Empty);

        return JsonSerializer.Serialize(message, RelayJsonContext.Default.MessageEnvelope);
    }
}
