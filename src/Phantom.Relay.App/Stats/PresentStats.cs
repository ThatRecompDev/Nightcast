// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.App.Stats;

/// <summary>
/// The render thread's side of the last stats window: from a picture's decode to the commit of the frame that first
/// shows it (average and worst), the CPU time a frame spent getting its picture on screen (the upload of a picture
/// read back, or the GPU path's access and draw; the commit itself not counted), and whether the pictures came on the
/// GPU.
/// </summary>
public readonly record struct PresentStats(TimeSpan DecodeToPresent, TimeSpan DecodeToPresentWorst, TimeSpan PictureCpu, bool OnGpu);
