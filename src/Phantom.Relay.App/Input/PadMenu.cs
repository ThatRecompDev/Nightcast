// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Input;

namespace Phantom.Relay.App.Input;

/// <summary>
/// Relay's settings on the pad alone, for a handheld without a keyboard (opened by <see cref="MenuChord"/>): the
/// D-pad moves between the rows (<see cref="MenuItem"/>), A or left or right changes the row's setting, B closes it.
/// Only presses count, so a button already down when it opens does nothing. It only says what was chosen; the app
/// does it and shows the rows (<see cref="Lines"/>).
/// </summary>
public sealed class PadMenu
{
    private static readonly string Title = $"{RelayApp.Name}  (D-pad: choose, A: change, B: back)";
    private const string Pointer = "> ";
    private const string Blank = "  ";
    private const int LabelWidth = 20;
    private const XboxButtons Change = XboxButtons.A | XboxButtons.DPadLeft | XboxButtons.DPadRight;

    private static readonly MenuItem[] Items =
    [
        MenuItem.Stats,
        MenuItem.FrameGeneration,
        MenuItem.LsfgSetting,
        MenuItem.Multiplier,
        MenuItem.Upscaler,
        MenuItem.Sharpness,
        MenuItem.PictureFit,
        MenuItem.RumbleStrength,
        MenuItem.Fullscreen,
        MenuItem.Record,
        MenuItem.Close,
        MenuItem.EndStream,
    ];

    private XboxButtons _previous;

    public bool IsOpen { get; private set; }

    /// <summary>The row the pointer is on.</summary>
    public MenuItem Selected => Items[Cursor];

    /// <summary>True when the last <see cref="Step"/> moved, chose or closed: the rows are shown again.</summary>
    public bool Changed { get; private set; }

    private int Cursor { get; set; }

    /// <summary>Opens on the row it was left on; <paramref name="held"/> are the buttons down now, which do nothing.</summary>
    public void Open(XboxButtons held)
    {
        IsOpen = true;
        _previous = held;
        Changed = true;
    }

    public void Close()
    {
        IsOpen = false;
        Changed = true;
    }

    /// <summary>One frame of the pad's buttons: the row chosen in it, or null.</summary>
    public MenuItem? Step(XboxButtons buttons)
    {
        var pressed = buttons & ~_previous;
        _previous = buttons;
        Changed = false;

        if (!IsOpen || pressed == XboxButtons.None)
        {
            return null;
        }

        Changed = true;

        if ((pressed & XboxButtons.B) != 0)
        {
            IsOpen = false;

            return null;
        }

        Move(pressed);

        if ((pressed & Change) != 0)
        {
            return Selected;
        }

        return null;
    }

    /// <summary>The menu as text: a title, then each row with its pointer, label and <paramref name="value"/>.</summary>
    public string[] Lines(Func<MenuItem, string> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var lines = new string[Items.Length + 1];
        lines[0] = Title;

        for (int index = 0; index < Items.Length; index++)
        {
            string pointer = Blank;

            if (index == Cursor)
            {
                pointer = Pointer;
            }

            lines[index + 1] = $"{pointer}{Label(Items[index]).PadRight(LabelWidth)}{value(Items[index])}";
        }

        return lines;
    }

    private void Move(XboxButtons pressed)
    {
        if ((pressed & XboxButtons.DPadUp) != 0)
        {
            Cursor = (Cursor + Items.Length - 1) % Items.Length;
        }

        if ((pressed & XboxButtons.DPadDown) != 0)
        {
            Cursor = (Cursor + 1) % Items.Length;
        }
    }

    private static string Label(MenuItem item)
    {
        return item switch
        {
            MenuItem.Stats => "Stats",
            MenuItem.FrameGeneration => "Frame generation",
            MenuItem.LsfgSetting => "LSFG setting",
            MenuItem.Multiplier => "Multiplier",
            MenuItem.Upscaler => "Upscaler",
            MenuItem.Sharpness => "Sharpness",
            MenuItem.PictureFit => "Picture",
            MenuItem.RumbleStrength => "Rumble strength",
            MenuItem.Fullscreen => "Fullscreen",
            MenuItem.Record => "Record",
            MenuItem.Close => "Back to the game",
            _ => "End the stream",
        };
    }
}
