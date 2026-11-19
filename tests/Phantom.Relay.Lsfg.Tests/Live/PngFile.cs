// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Phantom.Relay.Lsfg.Tests.Live;

/// <summary>Writes RGBA pixels as an 8-bit RGBA PNG (no filtering, zlib-deflated), to look at what a harness made.</summary>
internal static class PngFile
{
    private const int ColourTypeRgba = 6;
    private const int BitDepth = 8;
    private const uint CrcPolynomial = 0xEDB88320;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = MakeTable();

    public static void Write(string path, byte[] rgba, int width, int height)
    {
        using var file = File.Create(path);
        file.Write(Signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = BitDepth;
        header[9] = ColourTypeRgba;
        Chunk(file, "IHDR", header);

        Chunk(file, "IDAT", Deflate(rgba, width, height));
        Chunk(file, "IEND", []);
    }

    private static byte[] Deflate(byte[] rgba, int width, int height)
    {
        using var compressed = new MemoryStream();

        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            for (var row = 0; row < height; row++)
            {
                zlib.WriteByte(0);
                zlib.Write(rgba, row * width * 4, width * 4);
            }
        }

        return compressed.ToArray();
    }

    private static void Chunk(System.IO.Stream file, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        file.Write(length);

        var typed = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        file.Write(typed);

        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(typed));
        file.Write(crc);
    }

    private static uint Crc(byte[] bytes)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in bytes)
        {
            crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] MakeTable()
    {
        var table = new uint[256];

        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;

            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? CrcPolynomial ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
