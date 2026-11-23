// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Phantom.Engine.Core.Hashing;

namespace Phantom.Relay.Stream.Stills;

/// <summary>
/// Writes an 8-bit RGB picture as a PNG file (ISO/IEC 15948): the signature, IHDR (colour type 2, no interlace), one
/// IDAT of zlib-compressed rows, each with the Sub filter (a sample less the one a pixel to its left: the smooth
/// gradients of a game's picture compress far better so), and IEND; every chunk carries its CRC-32 over type and data.
/// The engine has a reader (<c>PngReader</c>) and no writer yet: an engine candidate, kept here until another app
/// needs it. Not for the render thread: a 2560x1440 picture takes a fraction of a second.
/// </summary>
public static class PngWriter
{
    /// <summary>The bytes of a pixel: red, green, blue.</summary>
    public const int BytesPerPixel = 3;

    private const int SignatureSize = 8;
    private const int LengthSize = 4;
    private const int TypeSize = 4;
    private const int CrcSize = 4;
    private const int HeaderSize = 13;
    private const byte BitDepth = 8;
    private const byte TrueColour = 2;
    private const byte SubFilter = 1;
    private const int DepthOffset = 8;
    private const int ColourOffset = 9;

    /// <summary>PNG's signature (section 5.2): 0x89, "PNG", CR LF, EOF, LF.</summary>
    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Writes the picture <paramref name="width"/> x <paramref name="height"/> whose rows, top first, are
    /// <paramref name="rgb"/> (<see cref="BytesPerPixel"/> bytes a pixel, no padding) to <paramref name="output"/>.
    /// </summary>
    public static void Write(System.IO.Stream output, int width, int height, ReadOnlySpan<byte> rgb)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfLessThan(rgb.Length, width * height * BytesPerPixel);

        output.Write(Signature, 0, SignatureSize);
        WriteChunk(output, Header(width, height));
        WriteChunk(output, ImageData(width, height, rgb));
        WriteChunk(output, Chunk("IEND"));
    }

    // IHDR: the size, 8 bits, true colour, deflate, filtering method 0, no interlace.
    private static MemoryStream Header(int width, int height)
    {
        var data = new byte[HeaderSize];
        BinaryPrimitives.WriteInt32BigEndian(data, width);
        BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(LengthSize), height);
        data[DepthOffset] = BitDepth;
        data[ColourOffset] = TrueColour;

        var chunk = Chunk("IHDR");
        chunk.Write(data);

        return chunk;
    }

    // IDAT: every row with its filter byte, compressed as one zlib stream.
    private static MemoryStream ImageData(int width, int height, ReadOnlySpan<byte> rgb)
    {
        var stride = width * BytesPerPixel;
        var row = new byte[stride + 1];
        row[0] = SubFilter;
        var chunk = Chunk("IDAT");

        using (var zlib = new ZLibStream(chunk, CompressionLevel.Optimal, leaveOpen: true))
        {
            for (var y = 0; y < height; y++)
            {
                Sub(rgb.Slice(y * stride, stride), row.AsSpan(1));
                zlib.Write(row);
            }
        }

        return chunk;
    }

    // The Sub filter (section 9.2): each byte less the byte of the same channel a pixel to its left, modulo 256.
    private static void Sub(ReadOnlySpan<byte> raw, Span<byte> filtered)
    {
        raw[..BytesPerPixel].CopyTo(filtered);

        for (var index = BytesPerPixel; index < raw.Length; index++)
        {
            filtered[index] = (byte)(raw[index] - raw[index - BytesPerPixel]);
        }
    }

    // A chunk being built: its type first, its data after, so the CRC runs over the buffer as it is.
    private static MemoryStream Chunk(string type)
    {
        var chunk = new MemoryStream();
        chunk.Write(Encoding.ASCII.GetBytes(type));

        return chunk;
    }

    private static void WriteChunk(System.IO.Stream output, MemoryStream chunk)
    {
        var body = chunk.GetBuffer().AsSpan(0, (int)chunk.Length);
        Span<byte> length = stackalloc byte[LengthSize];
        Span<byte> crc = stackalloc byte[CrcSize];
        BinaryPrimitives.WriteInt32BigEndian(length, body.Length - TypeSize);
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32.Hash(body));

        output.Write(length);
        output.Write(body);
        output.Write(crc);
        chunk.Dispose();
    }
}
