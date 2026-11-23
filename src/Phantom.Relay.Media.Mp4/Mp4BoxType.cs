// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Mp4;

/// <summary>
/// The four-character codes of the boxes and sample entries the writer uses, each as the big-endian 32-bit number its
/// four ASCII characters make (ISO/IEC 14496-12 unless noted).
/// </summary>
internal static class Mp4BoxType
{
    /// <summary>'ftyp': the file's brands.</summary>
    public const uint FileType = 0x66747970;

    /// <summary>'moov': the movie's description.</summary>
    public const uint Movie = 0x6D6F6F76;

    /// <summary>'mvhd': the movie's header.</summary>
    public const uint MovieHeader = 0x6D766864;

    /// <summary>'trak': one track.</summary>
    public const uint Track = 0x7472616B;

    /// <summary>'tkhd': a track's header.</summary>
    public const uint TrackHeader = 0x746B6864;

    /// <summary>'mdia': a track's media.</summary>
    public const uint Media = 0x6D646961;

    /// <summary>'mdhd': the media's header (its timescale).</summary>
    public const uint MediaHeader = 0x6D646864;

    /// <summary>'hdlr': the media's handler (video or sound).</summary>
    public const uint Handler = 0x68646C72;

    /// <summary>'minf': the media's information.</summary>
    public const uint MediaInformation = 0x6D696E66;

    /// <summary>'vmhd': a video track's media header.</summary>
    public const uint VideoMediaHeader = 0x766D6864;

    /// <summary>'smhd': a sound track's media header.</summary>
    public const uint SoundMediaHeader = 0x736D6864;

    /// <summary>'dinf': where the media's data is.</summary>
    public const uint DataInformation = 0x64696E66;

    /// <summary>'dref': the data references.</summary>
    public const uint DataReference = 0x64726566;

    /// <summary>'url ': a data reference to this file.</summary>
    public const uint DataEntryUrl = 0x75726C20;

    /// <summary>'stbl': the sample table (empty in a fragmented file: the samples are in the fragments).</summary>
    public const uint SampleTable = 0x7374626C;

    /// <summary>'stsd': the sample descriptions.</summary>
    public const uint SampleDescription = 0x73747364;

    /// <summary>'stts': decoding times.</summary>
    public const uint TimeToSample = 0x73747473;

    /// <summary>'stsc': samples to chunks.</summary>
    public const uint SampleToChunk = 0x73747363;

    /// <summary>'stsz': sample sizes.</summary>
    public const uint SampleSize = 0x7374737A;

    /// <summary>'stco': chunk offsets.</summary>
    public const uint ChunkOffset = 0x7374636F;

    /// <summary>'avc1': an H.264 sample entry, its parameter sets in its avcC (ISO/IEC 14496-15).</summary>
    public const uint Avc1 = 0x61766331;

    /// <summary>'avcC': the H.264 decoder configuration (ISO/IEC 14496-15 5.3.3.1).</summary>
    public const uint AvcConfiguration = 0x61766343;

    /// <summary>'Opus': an Opus sample entry (Encapsulation of Opus in ISO Base Media File Format, 4.3.1).</summary>
    public const uint Opus = 0x4F707573;

    /// <summary>'dOps': the Opus specific box (the same, 4.3.2).</summary>
    public const uint OpusSpecific = 0x644F7073;

    /// <summary>'mvex': the movie is fragmented.</summary>
    public const uint MovieExtends = 0x6D766578;

    /// <summary>'mehd': the fragmented movie's whole duration.</summary>
    public const uint MovieExtendsHeader = 0x6D656864;

    /// <summary>'trex': a track's fragment defaults.</summary>
    public const uint TrackExtends = 0x74726578;

    /// <summary>'moof': a movie fragment.</summary>
    public const uint MovieFragment = 0x6D6F6F66;

    /// <summary>'mfhd': a fragment's header (its sequence number).</summary>
    public const uint MovieFragmentHeader = 0x6D666864;

    /// <summary>'traf': one track's part of a fragment.</summary>
    public const uint TrackFragment = 0x74726166;

    /// <summary>'tfhd': a track fragment's header.</summary>
    public const uint TrackFragmentHeader = 0x74666864;

    /// <summary>'tfdt': a track fragment's first decoding time.</summary>
    public const uint TrackFragmentDecodeTime = 0x74666474;

    /// <summary>'trun': a run of samples.</summary>
    public const uint TrackRun = 0x7472756E;

    /// <summary>'mdat': the samples' bytes.</summary>
    public const uint MediaData = 0x6D646174;

    /// <summary>'mfra': the fragments' random access index, at the end.</summary>
    public const uint FragmentRandomAccess = 0x6D667261;

    /// <summary>'tfra': one track's random access points.</summary>
    public const uint TrackFragmentRandomAccess = 0x74667261;

    /// <summary>'mfro': the index's size, last in the file.</summary>
    public const uint FragmentRandomAccessOffset = 0x6D66726F;

    /// <summary>Brand 'isom'.</summary>
    public const uint BrandIsom = 0x69736F6D;

    /// <summary>Brand 'iso6': movie fragments with tfdt (the edition that has them).</summary>
    public const uint BrandIso6 = 0x69736F36;

    /// <summary>Brand 'mp41'.</summary>
    public const uint BrandMp41 = 0x6D703431;

    /// <summary>Handler 'vide'.</summary>
    public const uint HandlerVideo = 0x76696465;

    /// <summary>Handler 'soun'.</summary>
    public const uint HandlerSound = 0x736F756E;
}
