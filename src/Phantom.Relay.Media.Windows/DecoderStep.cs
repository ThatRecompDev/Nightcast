// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Media.Windows;

/// <summary>The step of <see cref="MediaFoundationH264Decoder"/> a failure is reported at.</summary>
internal enum DecoderStep
{
    Input = 0,
    ProcessInput = 1,
    OutputType = 2,
    ProcessOutput = 3,
    Surface = 4,
}
