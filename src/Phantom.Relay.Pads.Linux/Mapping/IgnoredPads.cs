// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;

namespace Phantom.Relay.Pads.Linux.Mapping;

/// <summary>
/// The pads a program must not read itself, as Steam lists them when it starts a game: the physical pads Steam Input
/// already turns into its virtual pad, in SDL's hint <c>SDL_GAMECONTROLLER_IGNORE_DEVICES</c>
/// (<c>0x28DE/0x1205,0x045E/0x028E</c>: vendor/product pairs in hex, comma-separated). Without it a pad would be read
/// twice, once as itself and once through Steam's virtual pad. An entry that does not read is skipped.
/// </summary>
internal sealed class IgnoredPads
{
    /// <summary>The name of the environment variable Steam sets (read by the head and handed in).</summary>
    public const string Variable = "SDL_GAMECONTROLLER_IGNORE_DEVICES";

    private const string HexPrefix = "0x";

    /// <summary>A vendor and a product.</summary>
    private const int PairParts = 2;

    private readonly HashSet<uint> _pairs;

    private IgnoredPads(HashSet<uint> pairs)
    {
        _pairs = pairs;
    }

    /// <summary>Nothing ignored.</summary>
    public static IgnoredPads None { get; } = new([]);

    /// <summary>The pairs <paramref name="hint"/> lists; none for null or empty.</summary>
    public static IgnoredPads Parse(string? hint)
    {
        var pairs = new HashSet<uint>();

        if (string.IsNullOrWhiteSpace(hint))
        {
            return new IgnoredPads(pairs);
        }

        foreach (var entry in hint.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split('/');

            if (parts.Length == PairParts && TryHex(parts[0], out var vendor) && TryHex(parts[1], out var product))
            {
                pairs.Add(Key(vendor, product));
            }
        }

        return new IgnoredPads(pairs);
    }

    public bool Contains(ushort vendor, ushort product)
    {
        return _pairs.Contains(Key(vendor, product));
    }

    private static bool TryHex(string text, out ushort value)
    {
        var digits = text.Trim();

        if (digits.StartsWith(HexPrefix, StringComparison.OrdinalIgnoreCase))
        {
            digits = digits[HexPrefix.Length..];
        }

        return ushort.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    private static uint Key(ushort vendor, ushort product)
    {
        return ((uint)vendor << (sizeof(ushort) * Events.EvdevCodes.BitsPerByte)) | product;
    }
}
