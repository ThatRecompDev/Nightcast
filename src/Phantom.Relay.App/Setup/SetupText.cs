// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Numerics;
using Microsoft.Extensions.Logging;
using Phantom.Engine.Core;
using Phantom.Engine.Core.Memory;
using Phantom.Engine.Graphics.Canvas;
using Phantom.Engine.Graphics.Devices;
using Phantom.Engine.Hosting;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App.Logging;
using Phantom.Relay.App.Stats;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// The setup's lines, large and centred in the window (each line centred, the block in the middle), drawn through the
/// engine's canvas and text as the stats are (<see cref="StatsOverlay"/>), so they read from a sofa or on a handheld's
/// screen. The em is a <see cref="ScreenLines"/>th of the window's height, smaller when the widest line would not fit
/// the window's width; a new em makes a new glyph set when new lines come (<see cref="Show"/>), never in a frame.
/// Without a font it draws nothing and says so once (the lines are on the text console too).
/// </summary>
public sealed class SetupText
{
    /// <summary>NOT DATA: the em is the window's height over this.</summary>
    public const float ScreenLines = 28f;

    /// <summary>NOT DATA: the smallest em, in frame pixels.</summary>
    public const float MinimumEm = 14f;

    /// <summary>NOT DATA: the share of the window's width the widest line may take.</summary>
    public const float WidthShare = 0.92f;

    private const int Vertices = 16384;
    private const int Commands = 16;
    private const float Half = 0.5f;

    /// <summary>The faces asked for, in order: the UI's face reads best at size, a monospaced one if it is missing.</summary>
    private static readonly string[] Faces = ["Segoe UI", "Consolas"];

    private static readonly Vector4 TextColour = new(0.95f, 0.95f, 0.95f, 1f);
    private static readonly TextStyle Style = TextStyle.Plain(TextColour);
    private static readonly string Characters = StatsOverlay.PrintableAscii();

    private readonly IGraphicsDevice _device;
    private readonly IWindow _window;
    private readonly ISystemFonts _fonts;
    private readonly ILogger _log;
    private readonly CanvasBatch _batch = new(Vertices, Commands);
    private CanvasRenderer? _canvas;
    private TrueTypeFont? _font;
    private GlyphSet? _set;
    private CanvasTexture _texture;
    private string[] _lines = [];
    private Vector2[] _origins = [];

    public SetupText(IGraphicsDevice device, IWindow window, ISystemFonts fonts, ILogger log)
    {
        _device = device;
        _window = window;
        _fonts = fonts;
        _log = log;
    }

    /// <summary>The em the glyph set was made at, in frame pixels; 0 before one was made.</summary>
    public float Em { get; private set; }

    /// <summary>The em for a window <paramref name="height"/> frame pixels high, on a whole pixel.</summary>
    public static float EmFor(int height)
    {
        return MathF.Round(Math.Max(MinimumEm, height / ScreenLines));
    }

    /// <summary>The em that fits a line <paramref name="widest"/> pixels wide at <paramref name="em"/> into <paramref name="width"/>.</summary>
    public static float FittedEm(float em, float widest, int width)
    {
        var room = width * WidthShare;

        if (widest <= room || widest <= 0f)
        {
            return em;
        }

        return MathF.Floor(Math.Max(MinimumEm, em * room / widest));
    }

    /// <summary>Makes the canvas and reads the font; without one nothing is ever drawn (logged once).</summary>
    public void Initialize()
    {
        _canvas = new CanvasRenderer(_device, Vertices, new NativeBlockAllocator());
        _canvas.Initialize();
        _font = OverlayFont.Read(_fonts, Faces, out _);

        if (_font is null)
        {
            _log.SetupFontMissing();
        }
    }

    /// <summary>Lays out new lines, the glyph set made again first when the window or the widest line asks for another em.</summary>
    public void Show(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (_font is null || _canvas is null)
        {
            return;
        }

        _lines = lines;
        Use(EmFor(_window.Height));
        var fitted = FittedEm(Em, Widest(), _window.Width);

        if (fitted != Em)
        {
            Use(fitted);
        }

        Layout();
    }

    /// <summary>The lines over what the open frame pass holds.</summary>
    [HotPath]
    public void Draw()
    {
        if (_canvas is null || _set is null || _lines.Length == 0)
        {
            return;
        }

        _batch.Begin(_window.Width, _window.Height);
        _batch.UseBlend(CanvasBlend.SourceOver);

        for (var index = 0; index < _lines.Length; index++)
        {
            TextPainter.Draw(_batch, _set, _texture, _lines[index], _origins[index], Style, 1f);
        }

        _canvas.Draw(_batch);
    }

    public void Cleanup()
    {
        _canvas?.Cleanup();
        _canvas = null;
        _set = null;
        _device.DestroyRetired();
    }

    private void Use(float em)
    {
        if (em == Em && _set is not null)
        {
            return;
        }

        if (_set is not null)
        {
            _canvas!.Release(_texture);
        }

        _set = GlyphAtlas.Build(_font!, em, Characters, UiFonts.AtlasWidth);
        _texture = _canvas!.Register(_set.Image);
        Em = em;
    }

    private float Widest()
    {
        var widest = 0f;

        foreach (var line in _lines)
        {
            widest = Math.Max(widest, TextPainter.Measure(_set!, line));
        }

        return widest;
    }

    /// <summary>Each line's baseline origin: centred across, the block centred down.</summary>
    private void Layout()
    {
        var set = _set!;
        var top = (_window.Height - (_lines.Length * set.LineHeight)) * Half;
        _origins = new Vector2[_lines.Length];

        for (var index = 0; index < _lines.Length; index++)
        {
            var width = TextPainter.Measure(set, _lines[index]);
            var x = MathF.Round((_window.Width - width) * Half);
            var y = MathF.Round(top + set.Ascent + (index * set.LineHeight));
            _origins[index] = new Vector2(x, y);
        }
    }
}
