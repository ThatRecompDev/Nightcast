// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Buffers.Binary;
using System.Text;

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>
/// A box read back from a written file, for the tests: its type, where it is and its bytes; containers list their
/// children.
/// </summary>
public sealed record Box(string Type, int Offset, int Length, byte[] File)
{
    private static readonly HashSet<string> Containers = ["moov", "trak", "mdia", "minf", "dinf", "stbl", "mvex", "moof", "traf", "mfra"];

    /// <summary>The bytes after the header.</summary>
    public ReadOnlySpan<byte> Payload => File.AsSpan(Offset + 8, Length - 8);

    /// <summary>The boxes at the top of <paramref name="file"/>.</summary>
    public static List<Box> Read(byte[] file)
    {
        return ReadRange(file, 0, file.Length);
    }

    /// <summary>The boxes inside this container.</summary>
    public List<Box> Children()
    {
        Assert.Contains(Type, Containers);

        return After(0);
    }

    /// <summary>The boxes inside this one after <paramref name="skip"/> bytes of its own fields (stsd, a sample entry).</summary>
    public List<Box> After(int skip)
    {
        return ReadRange(File, Offset + 8 + skip, Offset + Length);
    }

    /// <summary>The one child box of <paramref name="type"/>.</summary>
    public Box Child(string type)
    {
        return Assert.Single(Children(), box => box.Type == type);
    }

    /// <summary>The box at a path of types below this one, as "mdia/minf/stbl".</summary>
    public Box At(string path)
    {
        var box = this;

        foreach (var type in path.Split('/'))
        {
            box = box.Child(type);
        }

        return box;
    }

    public uint U32(int at)
    {
        return BinaryPrimitives.ReadUInt32BigEndian(Payload[at..]);
    }

    public ulong U64(int at)
    {
        return BinaryPrimitives.ReadUInt64BigEndian(Payload[at..]);
    }

    public ushort U16(int at)
    {
        return BinaryPrimitives.ReadUInt16BigEndian(Payload[at..]);
    }

    private static List<Box> ReadRange(byte[] file, int start, int end)
    {
        var boxes = new List<Box>();

        for (var at = start; at < end;)
        {
            var length = (int)BinaryPrimitives.ReadUInt32BigEndian(file.AsSpan(at));
            var type = Encoding.ASCII.GetString(file, at + 4, 4);
            Assert.True(length >= 8 && at + length <= end, $"box {type} at {at} has a bad length {length}");
            boxes.Add(new Box(type, at, length, file));
            at += length;
        }

        return boxes;
    }
}
