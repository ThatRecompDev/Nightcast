// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Ui.Text;

namespace Phantom.Relay.App.Tests.Stats;

/// <summary>A system that gives a file for every face, none of which is a font.</summary>
public sealed class UnreadableFonts : ISystemFonts
{
    public int PixelsPerInch => NoSystemFonts.StandardPixelsPerInch;

    public SystemFont? Find(string face, bool bold, bool italic)
    {
        return new SystemFont(face, "broken.ttf", [1, 2, 3, 4], bold, italic);
    }
}
