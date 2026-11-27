// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using Phantom.Engine.Core;

namespace Phantom.Relay.Xbox.Input;

/// <summary>
/// Recognises the console's server metadata on the input channel, its answer to a pad it took: a report whose
/// <c>u16 kind</c> is <see cref="InputReportKind.ServerMetadata"/> (input protocol 8, seen live 2026-11-26).
/// </summary>
public static class ServerMetadataReport
{
    private const int KindSize = sizeof(ushort);

    [HotPath]
    public static bool Is(ReadOnlySpan<byte> data)
    {
        return data.Length >= KindSize && BinaryPrimitives.ReadUInt16LittleEndian(data) == (ushort)InputReportKind.ServerMetadata;
    }
}
