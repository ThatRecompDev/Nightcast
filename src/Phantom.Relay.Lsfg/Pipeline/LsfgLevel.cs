// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Lsfg.Pipeline;

/// <summary>One chain on one flow level: its correlation's outputs, its steps' outputs but the last, and its result.</summary>
internal sealed record LsfgLevel(GpuTexture[] Correlations, GpuTexture[][] Temporaries, GpuTexture Result);
