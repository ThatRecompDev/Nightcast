// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Engine.Core;
using Phantom.Engine.Hosting.Input;

namespace Phantom.Relay.Profiles.KeyboardMouse;

/// <summary>A pointer button's bit in the engine's held-buttons set: one bit per button, Left the lowest.</summary>
public static class PointerButtonBits
{
    [HotPath]
    public static PointerButtons Of(PointerButton button)
    {
        if (button <= PointerButton.None || button > PointerButton.Button8)
        {
            return PointerButtons.None;
        }

        return (PointerButtons)(1 << ((int)button - 1));
    }
}
