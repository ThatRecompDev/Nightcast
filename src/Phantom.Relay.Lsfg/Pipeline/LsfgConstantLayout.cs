// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Lsfg.Shaders;

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>
/// The constant fields the shaders declare, with the byte offsets D3DReflect reports for them in every shader of
/// the frame-generation set (ids 255 to 302 of the Lossless.dll this was written against). A shader whose buffer
/// names another field or puts one elsewhere is refused, so a changed DLL fails loudly instead of misreading.
/// </summary>
internal static class LsfgConstantLayout
{
    /// <summary>The bytes of <see cref="LsfgConstants"/>.</summary>
    public const int Size = 48;

    // Reflected offsets (D3DReflect, ID3D11ShaderReflectionVariable.GetDesc StartOffset).
    public const int InputOffsetOffset = 0;
    public const int FirstIterOffset = 8;
    public const int FirstIterSOffset = 12;
    public const int AdvancedColorKindOffset = 16;
    public const int HdrSupportOffset = 20;
    public const int ResolutionInvScaleOffset = 24;
    public const int TimestampOffset = 28;
    public const int UiThresholdOffset = 32;

    // The offset of a field the layout does not know.
    private const int UnknownOffset = -1;

    /// <summary>The UI mask's threshold, as public descriptions of the pipeline give it (only the beta mask step reads it).</summary>
    public const float UiThreshold = 0.5f;

    /// <summary>AdvancedColorKind for an SDR picture: the mipmaps pass then reads colour as is (2 would be HDR).</summary>
    public const uint StandardDynamicRange = 0;

    /// <summary>The problem with a shader's constant buffer, or null when it matches this layout.</summary>
    public static string? Check(ShaderLayout layout)
    {
        if (layout.ConstantBufferSize > Size)
        {
            return $"shader {layout.Id} has a {layout.ConstantBufferSize}-byte constant buffer; the pipeline knows {Size} bytes";
        }

        foreach (var constant in layout.Constants)
        {
            var expected = OffsetOf(constant.Name);

            if (expected != constant.Offset)
            {
                return $"shader {layout.Id} has constant '{constant.Name}' at byte {constant.Offset}; the pipeline expects {expected}";
            }
        }

        return null;
    }

    private static int OffsetOf(string name)
    {
        return name switch
        {
            "InputOffset" => InputOffsetOffset,
            "FirstIter" => FirstIterOffset,
            "FirstIterS" => FirstIterSOffset,
            "AdvancedColorKind" => AdvancedColorKindOffset,
            "HdrSupport" => HdrSupportOffset,
            "ResolutionInvScale" => ResolutionInvScaleOffset,
            "Timestamp" => TimestampOffset,
            "UiThreshold" => UiThresholdOffset,
            _ => UnknownOffset,
        };
    }
}
