// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Stream.Media;

/// <summary>How the stream's 16:9 picture fills a window of another shape (a 16:10 handheld's screen).</summary>
public enum PictureFit
{
    /// <summary>As large as fits, centred, with bars where the shapes differ; nothing cut, nothing stretched.</summary>
    Bars,

    /// <summary>The whole window, the picture stretched to its shape (about 11 % taller on 16:10).</summary>
    Stretch,
}
