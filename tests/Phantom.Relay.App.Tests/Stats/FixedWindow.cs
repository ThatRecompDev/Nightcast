// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Hosting;
using Phantom.Engine.Imaging;

namespace Phantom.Relay.App.Tests.Stats;

/// <summary>A window of a fixed size and scale that does nothing else.</summary>
public sealed class FixedWindow(int width, int height, float dpiScale) : IWindow
{
    public int Width => width;

    public int Height => height;

    public float DpiScale => dpiScale;

    public bool MouseLocked => false;

    public bool CursorVisible => true;

    public int CursorSlotCount => 0;

    public void SetTitle(string title)
    {
    }

    public void Quit()
    {
    }

    public void LockMouse(bool locked)
    {
    }

    public void SetCursorVisible(bool visible)
    {
    }

    public void BindCursor(int slot, TextureImage image, int hotspotX, int hotspotY)
    {
    }

    public void ShowCursor(int slot)
    {
    }

    public void ShowSystemCursor()
    {
    }

    public void Hide()
    {
    }
}
