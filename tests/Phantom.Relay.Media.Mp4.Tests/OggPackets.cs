// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4.Tests;

/// <summary>
/// The packets of an Ogg file (RFC 3533): each page's segments joined, a packet that fills a segment (255) going on
/// into the next one or the next page. For the live check's Opus made by ffmpeg; the first two are OpusHead and OpusTags.
/// </summary>
public static class OggPackets
{
    private const int HeaderLength = 27;
    private const int SegmentCountAt = 26;
    private const int FullSegment = 255;

    public static List<byte[]> Read(byte[] file)
    {
        var packets = new List<byte[]>();
        var current = new List<byte>();

        for (var page = 0; page < file.Length;)
        {
            Assert.Equal("OggS"u8.ToArray(), file.AsSpan(page, 4).ToArray());
            var segments = file[page + SegmentCountAt];
            var data = page + HeaderLength + segments;

            for (var index = 0; index < segments; index++)
            {
                var length = file[page + HeaderLength + index];
                current.AddRange(file.AsSpan(data, length));
                data += length;

                if (length < FullSegment)
                {
                    packets.Add([.. current]);
                    current.Clear();
                }
            }

            page = data;
        }

        return packets;
    }
}
