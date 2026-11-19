// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core.Randomness;

namespace Phantom.Relay.Xbox.Channels;

/// <summary>Message and installation ids (GUID text) drawn from the injected random source, so a test can replay them.</summary>
public static class MessageIds
{
    private const int GuidBytes = 16;
    private const int WordBytes = sizeof(uint);

    public static string Next(IRandomSource random)
    {
        Span<byte> bytes = stackalloc byte[GuidBytes];

        for (int offset = 0; offset < GuidBytes; offset += WordBytes)
        {
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..], random.NextUInt32());
        }

        return new Guid(bytes).ToString();
    }

    public static IReadOnlyList<string> Next(IRandomSource random, int count)
    {
        var ids = new string[count];

        for (int i = 0; i < count; i++)
        {
            ids[i] = Next(random);
        }

        return ids;
    }
}
