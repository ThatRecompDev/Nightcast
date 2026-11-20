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

namespace Phantom.Relay.App.Stats;

/// <summary>
/// The stats as a small block in the window's top-left corner: a few short lines (<see cref="StatsText.Lines"/>) on a
/// translucent dark panel, drawn over the picture in the frame's pass through the engine's canvas. The font is the
/// system's (a monospaced face first), read once at <see cref="Initialize"/> and made into one glyph set of printable
/// ASCII (and the recorder's two marks) at an em of <see cref="BaseEm"/> logical pixels times the window's scale, smaller in a small window; a window
/// whose size or scale asks for another em gets a new set when the next lines come (<see cref="Show"/>, once a
/// second), never in a frame. Without a font it draws nothing and says so once. A frame's <see cref="Draw"/> fills one
/// batch from the lines already laid out and allocates nothing.
/// </summary>
public sealed class StatsOverlay
{
    /// <summary>The em of the lines in logical pixels, at 100% scaling.</summary>
    public const float BaseEm = 14f;

    /// <summary>The smallest em the lines are drawn at, in frame pixels.</summary>
    public const float MinimumEm = 11f;

    /// <summary>The em is at most the window's height over this, so the block stays small in a small window.</summary>
    public const float ScreenLines = 40f;

    // The batch's budget: the panel and every glyph of the longest lines, two triangles each.
    private const int Vertices = 8192;
    private const int Commands = 16;

    // The block's margin from the corner and the panel's padding around the lines, in logical pixels at the base em.
    private const float Margin = 8f;
    private const float Padding = 6f;
    private const float Twice = 2f;

    private const char FirstPrintable = ' ';
    private const char LastPrintable = '~';

    /// <summary>The recorder's marks beside ASCII: its dot ("● REC") and the ellipsis of a shortened folder (a face without them skips them).</summary>
    private const string RecorderMarks = "●…";

    /// <summary>The faces asked for, in order: a monospaced one keeps the numbers still, the UI's face if it is missing.</summary>
    private static readonly string[] Faces = ["Consolas", "Segoe UI"];

    private static readonly Vector4 PanelColour = new(0f, 0f, 0f, 0.6f);
    private static readonly Vector4 TextColour = new(0.95f, 0.95f, 0.95f, 1f);
    private static readonly TextStyle Style = TextStyle.Plain(TextColour);
    private static readonly string Printable = PrintableAscii();

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
    private Vector2 _panelMin;
    private Vector2 _panelMax;
    private Vector2 _firstBaseline;

    public StatsOverlay(IGraphicsDevice device, IWindow window, ISystemFonts fonts, ILogger log)
    {
        _device = device;
        _window = window;
        _fonts = fonts;
        _log = log;
    }

    /// <summary>Whether a font was found, so the block is drawn.</summary>
    public bool Available => _font is not null;

    /// <summary>The em the glyph set was made at, in frame pixels; 0 before one was made.</summary>
    public float Em { get; private set; }

    /// <summary>
    /// The em for a window of <paramref name="height"/> frame pixels at <paramref name="dpiScale"/>: the base em
    /// scaled, at most a <see cref="ScreenLines"/>th of the height, at least <see cref="MinimumEm"/>, on a whole pixel.
    /// </summary>
    public static float EmFor(float dpiScale, int height)
    {
        float scale = dpiScale > 0f ? dpiScale : 1f;
        float em = Math.Min(BaseEm * scale, height / ScreenLines);

        return MathF.Round(Math.Max(MinimumEm, em));
    }

    /// <summary>Makes the canvas and reads the font; without one the block is never drawn (logged once).</summary>
    public void Initialize()
    {
        _canvas = new CanvasRenderer(_device, Vertices, new NativeBlockAllocator());
        _canvas.Initialize();
        _font = ReadFont(out string face);

        if (_font is null)
        {
            _log.OverlayFontMissing();

            return;
        }

        Build(EmFor(_window.DpiScale, _window.Height));
        _log.OverlayFont(face, Em);
    }

    /// <summary>Lays out new lines (a new stats snapshot), the glyph set made again first when the window asks for another em.</summary>
    public void Show(string[] lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (_font is null)
        {
            return;
        }

        float em = EmFor(_window.DpiScale, _window.Height);

        if (em != Em)
        {
            Build(em);
        }

        _lines = lines;
        Layout();
    }

    /// <summary>Forgets the lines, so the block comes back only with new ones.</summary>
    public void Clear()
    {
        _lines = [];
    }

    /// <summary>The panel and its lines over what the open frame pass holds.</summary>
    [HotPath]
    public void Draw()
    {
        if (_canvas is null || _set is null || _lines.Length == 0)
        {
            return;
        }

        _batch.Begin(_window.Width, _window.Height);
        _batch.UseBlend(CanvasBlend.SourceOver);
        _batch.Use(CanvasTexture.White, CanvasSampling.Nearest, CanvasFill.Texture);
        _batch.Rectangle(_panelMin, _panelMax, Vector2.Zero, Vector2.One, PanelColour, Vector4.Zero);

        var origin = _firstBaseline;

        foreach (var line in _lines)
        {
            TextPainter.Draw(_batch, _set, _texture, line, origin, Style, 1f);
            origin.Y += _set.LineHeight;
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

    /// <summary>The first face the system gives a file for that reads as a TrueType font; null when none does.</summary>
    private TrueTypeFont? ReadFont(out string face)
    {
        foreach (var name in Faces)
        {
            var file = _fonts.Find(name, false, false);

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

    /// <summary>The glyph set at <paramref name="em"/>, its texture registered; the one before it released.</summary>
    private void Build(float em)
    {
        if (_set is not null)
        {
            _canvas!.Release(_texture);
        }

        _set = GlyphAtlas.Build(_font!, em, Printable, UiFonts.AtlasWidth);
        _texture = _canvas!.Register(_set.Image);
        Em = em;
    }

    /// <summary>The panel around the lines and the first line's baseline, in frame pixels.</summary>
    private void Layout()
    {
        var set = _set!;
        float unit = Em / BaseEm;
        float widest = 0f;

        foreach (var line in _lines)
        {
            widest = Math.Max(widest, TextPainter.Measure(set, line));
        }

        float padding = MathF.Round(Padding * unit);
        _panelMin = new Vector2(MathF.Round(Margin * unit));
        _panelMax = _panelMin + new Vector2(MathF.Ceiling(widest) + (Twice * padding), (_lines.Length * set.LineHeight) + (Twice * padding));
        _firstBaseline = _panelMin + new Vector2(padding, padding + set.Ascent);
    }

    private static string PrintableAscii()
    {
        var characters = new char[LastPrintable - FirstPrintable + 1];

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = (char)(FirstPrintable + index);
        }

        return new string(characters) + RecorderMarks;
    }
}
