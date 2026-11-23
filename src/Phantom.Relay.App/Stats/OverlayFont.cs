// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Ui.Text;

namespace Phantom.Relay.App.Stats;

/// <summary>The font the window's text is drawn with: the first face asked for that the system gives a TrueType file for.</summary>
public static class OverlayFont
{
    /// <summary>The first of <paramref name="faces"/> the system gives a file for that reads as a TrueType font; null when none does.</summary>
    public static TrueTypeFont? Read(ISystemFonts fonts, IReadOnlyList<string> faces, out string face)
    {
        ArgumentNullException.ThrowIfNull(fonts);
        ArgumentNullException.ThrowIfNull(faces);

        foreach (var name in faces)
        {
            var file = fonts.Find(name, false, false);

            if (file is null)
            {
                continue;
            }

            try
            {
                face = file.Face;

                return TrueTypeFont.Read(file.Data);
            }
            catch (InvalidDataException)
            {
                // A collection or a file the reader does not take: the next face.
            }
        }

        face = string.Empty;

        return null;
    }
}
