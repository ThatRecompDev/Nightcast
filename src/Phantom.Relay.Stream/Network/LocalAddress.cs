// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Phantom.Relay.Stream.Network;

/// <summary>
/// This machine's IPv4 address on the network that has the default gateway: the address our host candidate
/// announces. Read from the platform, so a head calls it and hands the address to the stream; the WebRTC layer never
/// reads it. Desktop only: the browser and the PS5 heads find theirs another way.
/// </summary>
public static class LocalAddress
{
    private const int OctetBits = 8;

    public static uint? Find()
    {
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (network.OperationalStatus != OperationalStatus.Up || network.NetworkInterfaceType == NetworkInterfaceType.Loopback)
            {
                continue;
            }

            var properties = network.GetIPProperties();

            if (properties.GatewayAddresses.Count == 0)
            {
                continue;
            }

            foreach (var unicast in properties.UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ToUInt(unicast.Address.GetAddressBytes());
                }
            }
        }

        return null;
    }

    private static uint ToUInt(byte[] octets)
    {
        uint address = 0;

        foreach (byte octet in octets)
        {
            address = (address << OctetBits) | octet;
        }

        return address;
    }
}
