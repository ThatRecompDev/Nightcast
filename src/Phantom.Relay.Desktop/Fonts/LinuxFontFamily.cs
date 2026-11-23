// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Fonts;

/// <summary>
/// One font family as its TrueType files are named where Linux distributions install them: the regular cut and, where
/// the family has them, its bold, italic and bold italic cuts (null for a cut it lacks, which the renderer then makes up
/// from another).
/// </summary>
internal sealed record LinuxFontFamily(string Face, string Regular, string? Bold, string? Italic, string? BoldItalic)
{
    /// <summary>The file names to try for a cut, the closest first; the regular cut always last.</summary>
    public IEnumerable<(string File, bool Bold, bool Italic)> Cuts(bool bold, bool italic)
    {
        if (bold && italic && BoldItalic is not null)
        {
            yield return (BoldItalic, true, true);
        }

        if (bold && Bold is not null)
        {
            yield return (Bold, true, false);
        }

        if (italic && Italic is not null)
        {
            yield return (Italic, false, true);
        }

        yield return (Regular, false, false);
    }
}
