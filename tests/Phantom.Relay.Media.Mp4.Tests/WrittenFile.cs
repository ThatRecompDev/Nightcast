// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>A writer into memory and what it wrote, read back as boxes, for the tests.</summary>
public sealed class WrittenFile : IDisposable
{
    private readonly MemoryStream _stream = new();

    public WrittenFile(Mp4AudioTrack? audio)
    {
        Writer = new FragmentedMp4Writer(_stream, SyntheticMedia.Video(), audio);
    }

    /// <summary>The moov a writer of <paramref name="audio"/> writes at once.</summary>
    public static Box Front(Mp4AudioTrack? audio)
    {
        using var file = new WrittenFile(audio);

        return Assert.Single(file.All("moov"));
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    public FragmentedMp4Writer Writer { get; }

    public byte[] Bytes => _stream.ToArray();

    /// <summary>The top-level boxes written so far.</summary>
    public List<Box> Boxes => Box.Read(Bytes);

    /// <summary>The top-level boxes of <paramref name="type"/>.</summary>
    public List<Box> All(string type)
    {
        return Boxes.Where(box => box.Type == type).ToList();
    }

    /// <summary>The traf of <paramref name="trackId"/> in <paramref name="fragment"/>, or null.</summary>
    public static Box? Track(Box fragment, uint trackId)
    {
        return fragment.Children().Where(box => box.Type == "traf").SingleOrDefault(traf => traf.Child("tfhd").U32(4) == trackId);
    }

    /// <summary>A trun's samples: duration, size and (for video) flags.</summary>
    public static List<(uint Duration, uint Size, uint Flags)> Samples(Box traf, bool withFlags)
    {
        var run = traf.Child("trun");
        var count = (int)run.U32(4);
        var stride = withFlags ? 12 : 8;
        var samples = new List<(uint, uint, uint)>();

        for (var index = 0; index < count; index++)
        {
            var at = 12 + (index * stride);
            var flags = withFlags ? run.U32(at + 8) : 0;
            samples.Add((run.U32(at), run.U32(at + 4), flags));
        }

        return samples;
    }

    /// <summary>The bytes of a traf's samples, from its trun's data offset (counted from the moof).</summary>
    public static byte[] Data(Box fragment, Box traf, bool withFlags)
    {
        var start = fragment.Offset + (int)traf.Child("trun").U32(8);
        var length = Samples(traf, withFlags).Sum(sample => (int)sample.Size);

        return fragment.File.AsSpan(start, length).ToArray();
    }
}
