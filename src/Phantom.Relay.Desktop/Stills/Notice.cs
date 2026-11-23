// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Desktop.Stills;

/// <summary>A line of the <see cref="NoticeBoard"/>: its text, when it was posted (the clock's timestamp) and for how long it shows.</summary>
internal sealed record Notice(string Text, long PostedAt, TimeSpan ShownFor);
