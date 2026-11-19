// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Media.Video;

namespace Phantom.Relay.Media.Windows;

/// <summary>
/// The colour an H.264 stream states in its sequence parameter set's VUI (H.264 7.3.2.1.1 and E.1.1): the matrix by
/// its matrix_coefficients (ITU-T H.273: 1 BT.709, 5 and 6 BT.601, 7 SMPTE 240M, 9 and 10 BT.2020, 0 GBR, the numbers
/// Phantom.Engine reads from Matroska's Colour and AV1's sequence header) and the range by video_full_range_flag. A
/// stream that says nothing leaves the matrix unknown (null) in limited range, which the player draws as BT.709.
/// Reading allocates nothing.
/// </summary>
public static class H264StreamColour
{
    private const int MatrixGbr = 0;
    private const int MatrixBt709 = 1;
    private const int MatrixBt470Bg = 5;
    private const int MatrixSmpte170 = 6;
    private const int MatrixSmpte240 = 7;
    private const int MatrixBt2020NonConstant = 9;
    private const int MatrixBt2020Constant = 10;

    private const int ChromaFormat444 = 3;
    private const int BitDepthCodes = 2;
    private const int CroppingOffsets = 4;
    private const int ScalingListsBelow444 = 8;
    private const int ScalingListsOf444 = 12;
    private const int SmallScalingLists = 6;
    private const int SmallScalingListSize = 16;
    private const int LargeScalingListSize = 64;
    private const int ScalingListStart = 8;
    private const int ScalingListModulus = 256;

    private const int ProfileIdcBits = 8;
    private const int ConstraintAndLevelBits = 16;
    private const int AspectRatioIdcBits = 8;
    private const int ExtendedSarIdc = 255;
    private const int SarBits = 32;
    private const int VideoFormatBits = 3;
    private const int ColourCodeBits = 8;

    // The profiles whose sequence parameter set carries chroma_format_idc and the bit depths (H.264 7.3.2.1.1).
    private static readonly int[] ProfilesWithChromaFormat = [100, 110, 122, 244, 44, 83, 86, 118, 128, 138, 139, 134, 135];

    /// <summary>
    /// Reads the colour of a sequence parameter set NAL unit (its header byte first, as <see cref="AnnexBUnits"/> gives
    /// it); false when the unit is not one or ends early. It runs once per parameter set (a key frame), not per frame, so
    /// it is not a hot path; its test still pins that it allocates nothing.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> sequenceParameterSet, out VideoColourSpace? space, out VideoRange range)
    {
        space = null;
        range = VideoRange.Limited;

        if (AnnexBUnits.TypeOf(sequenceParameterSet) != AnnexBUnits.SequenceParameterSetType)
        {
            return false;
        }

        var reader = new RbspBitReader(sequenceParameterSet[1..]);
        var hasVui = SkipToVui(ref reader);

        if (reader.Overran)
        {
            return false;
        }

        if (hasVui)
        {
            ReadVideoSignal(ref reader, out space, out range);
        }

        return !reader.Overran;
    }

    /// <summary>The matrix an H.273 matrix_coefficients value names; null for one the engine does not draw.</summary>
    public static VideoColourSpace? SpaceOf(int matrixCoefficients)
    {
        switch (matrixCoefficients)
        {
            case MatrixGbr:
                return VideoColourSpace.Rgb;
            case MatrixBt709:
                return VideoColourSpace.Bt709;
            case MatrixBt470Bg:
            case MatrixSmpte170:
                return VideoColourSpace.Bt601;
            case MatrixSmpte240:
                return VideoColourSpace.Smpte240;
            case MatrixBt2020NonConstant:
            case MatrixBt2020Constant:
                return VideoColourSpace.Bt2020;
            default:
                return null;
        }
    }

    /// <summary>What a picture is drawn with: the stream's matrix, else BT.709, in the stream's range.</summary>
    public static VideoColour Settle(VideoColourSpace? space, VideoRange range)
    {
        var matrix = space ?? VideoColourSpace.Bt709;

        return new VideoColour(matrix, range);
    }

    // seq_parameter_set_data up to vui_parameters_present_flag, which it returns.
    private static bool SkipToVui(ref RbspBitReader reader)
    {
        var profile = (int)reader.ReadBits(ProfileIdcBits);
        _ = reader.ReadBits(ConstraintAndLevelBits);
        _ = reader.ReadUnsignedExpGolomb();

        if (Array.IndexOf(ProfilesWithChromaFormat, profile) >= 0)
        {
            SkipChromaFormat(ref reader);
        }

        // log2_max_frame_num_minus4, then the picture order count.
        _ = reader.ReadUnsignedExpGolomb();
        SkipPictureOrderCount(ref reader);

        // max_num_ref_frames, gaps_in_frame_num_value_allowed_flag, the size in macroblocks.
        _ = reader.ReadUnsignedExpGolomb();
        _ = reader.ReadBit();
        _ = reader.ReadUnsignedExpGolomb();
        _ = reader.ReadUnsignedExpGolomb();

        if (!reader.ReadFlag())
        {
            // mb_adaptive_frame_field_flag, present for a stream that is not frames only.
            _ = reader.ReadBit();
        }

        // direct_8x8_inference_flag, then the cropping.
        _ = reader.ReadBit();

        if (reader.ReadFlag())
        {
            SkipUnsigned(ref reader, CroppingOffsets);
        }

        return reader.ReadFlag();
    }

    private static void SkipChromaFormat(ref RbspBitReader reader)
    {
        var chromaFormat = reader.ReadUnsignedExpGolomb();

        if (chromaFormat == ChromaFormat444)
        {
            // separate_colour_plane_flag.
            _ = reader.ReadBit();
        }

        // bit_depth_luma_minus8, bit_depth_chroma_minus8, qpprime_y_zero_transform_bypass_flag.
        SkipUnsigned(ref reader, BitDepthCodes);
        _ = reader.ReadBit();

        if (!reader.ReadFlag())
        {
            return;
        }

        var lists = chromaFormat == ChromaFormat444 ? ScalingListsOf444 : ScalingListsBelow444;

        for (var list = 0; list < lists && !reader.Overran; list++)
        {
            if (!reader.ReadFlag())
            {
                continue;
            }

            var size = list < SmallScalingLists ? SmallScalingListSize : LargeScalingListSize;
            SkipScalingList(ref reader, size);
        }
    }

    // scaling_list() (H.264 7.3.2.1.1.1): delta_scale values until the list repeats its last.
    private static void SkipScalingList(ref RbspBitReader reader, int size)
    {
        var next = ScalingListStart;

        for (var index = 0; index < size && next != 0 && !reader.Overran; index++)
        {
            var delta = reader.ReadSignedExpGolomb();
            next = (next + delta + ScalingListModulus) % ScalingListModulus;
        }
    }

    private static void SkipPictureOrderCount(ref RbspBitReader reader)
    {
        var type = reader.ReadUnsignedExpGolomb();

        if (type == 0)
        {
            // log2_max_pic_order_cnt_lsb_minus4.
            _ = reader.ReadUnsignedExpGolomb();

            return;
        }

        if (type != 1)
        {
            return;
        }

        // delta_pic_order_always_zero_flag, offset_for_non_ref_pic, offset_for_top_to_bottom_field, the cycle.
        _ = reader.ReadBit();
        _ = reader.ReadSignedExpGolomb();
        _ = reader.ReadSignedExpGolomb();
        var cycle = reader.ReadUnsignedExpGolomb();

        for (var index = 0u; index < cycle && !reader.Overran; index++)
        {
            _ = reader.ReadSignedExpGolomb();
        }
    }

    // vui_parameters() up to the colour description (H.264 E.1.1).
    private static void ReadVideoSignal(ref RbspBitReader reader, out VideoColourSpace? space, out VideoRange range)
    {
        space = null;
        range = VideoRange.Limited;

        if (reader.ReadFlag() && reader.ReadBits(AspectRatioIdcBits) == ExtendedSarIdc)
        {
            _ = reader.ReadBits(SarBits);
        }

        if (reader.ReadFlag())
        {
            // overscan_appropriate_flag.
            _ = reader.ReadBit();
        }

        if (!reader.ReadFlag())
        {
            return;
        }

        _ = reader.ReadBits(VideoFormatBits);
        range = reader.ReadFlag() ? VideoRange.Full : VideoRange.Limited;

        if (!reader.ReadFlag())
        {
            return;
        }

        // colour_primaries and transfer_characteristics, then matrix_coefficients.
        _ = reader.ReadBits(ColourCodeBits);
        _ = reader.ReadBits(ColourCodeBits);
        space = SpaceOf((int)reader.ReadBits(ColourCodeBits));
    }

    private static void SkipUnsigned(ref RbspBitReader reader, int count)
    {
        for (var index = 0; index < count; index++)
        {
            _ = reader.ReadUnsignedExpGolomb();
        }
    }
}
