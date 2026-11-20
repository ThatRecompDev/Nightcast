// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Diagnostics;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Testing.Time;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Lsfg.Pacing;
using Phantom.Relay.Stream.Media.Gpu;
using Xunit.Abstractions;

namespace Phantom.Relay.Desktop.Tests.Pictures;

/// <summary>
/// The 120 fps mode's hand-over: x4 groups (three generated frames and the new picture) published every 33.3 ms, as
/// a 30 fps game gives them, into the decoder's ring (<see cref="GpuPictureSlots"/>, 2 x 4 + 1 slots) paced by the frame
/// pacer, and taken by a renderer refreshing at 240 Hz. Every slot claim must succeed, every frame be shown once and in
/// order, and the shown frames be 8.3 ms apart (two refreshes each). On a manual clock exactly; on the real clock
/// (Live) as a render loop meets it.
/// </summary>
public sealed class QuadrupledPacingTests
{
    private const int Multiplier = 4;
    private const int Slots = (2 * Multiplier) + 1;
    private const int Groups = 60;

    private static readonly TimeSpan Source = TimeSpan.FromSeconds(1.0 / 30);
    private static readonly TimeSpan Refresh = TimeSpan.FromSeconds(1.0 / 240);
    private static readonly TimeSpan GuessedRefresh = TimeSpan.FromSeconds(1.0 / 60);

    private readonly ITestOutputHelper _output;

    public QuadrupledPacingTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void X4_at_240_hz_shows_every_frame_once_in_order_two_refreshes_each()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        var shown = Run(clock, clock.Advance);

        // After the refresh estimate settles (a quarter of a second), every frame of every group is shown.
        var settled = shown.Where(frame => frame.Picture >= 10 * Multiplier).ToList();
        Assert.Equal((Groups - 10) * Multiplier, settled.Count);
        Assert.All(settled.Zip(settled.Skip(1)), pair => Assert.Equal(2, pair.Second.Refresh - pair.First.Refresh));
    }

    [Fact]
    [Trait("Category", "Live")]
    public void X4_at_240_hz_on_the_real_clock_shows_nearly_every_frame_in_order()
    {
        var shown = Run(TimeProvider.System, SpinFor);
        var settled = shown.Where(frame => frame.Picture >= 10 * Multiplier).ToList();
        var gaps = settled.Zip(settled.Skip(1)).Select(pair => pair.Second.Refresh - pair.First.Refresh).ToList();
        _output.WriteLine($"{settled.Count} of {(Groups - 10) * Multiplier} frames shown; refreshes between them: {string.Join(' ', gaps.GroupBy(gap => gap).OrderBy(group => group.Key).Select(group => $"{group.Key}x{group.Count()}"))}");

        Assert.True(settled.Count >= (Groups - 10) * Multiplier * 95 / 100, "more than one frame in twenty skipped");
    }

    // Groups published every source interval and a refresh every 1/240 s, in time order; the frames shown, numbered
    // picture by picture (group * 4 + step, the new picture last), with the refresh each was shown at.
    private static List<(long Picture, int Refresh)> Run(TimeProvider clock, Action<TimeSpan> wait)
    {
        var slots = new GpuPictureSlots(Slots, Multiplier, new NewestPicturePacer());
        var pacer = new LsfgPicturePacer(new FramePacer(clock, Multiplier, Source, GuessedRefresh));
        slots.UsePacer(pacer);
        var shown = new List<(long Picture, int Refresh)>();
        var refreshesPerGroup = (int)Math.Round(Source / Refresh);

        for (var refresh = 0; refresh < Groups * refreshesPerGroup; refresh++)
        {
            if (refresh % refreshesPerGroup == 0)
            {
                Publish(slots, pacer, refresh / refreshesPerGroup);
            }

            if (slots.TryTake(out var picture))
            {
                shown.Add((picture.DecodedAt, refresh));
                slots.Release(picture, default);
            }

            wait(Refresh);
        }

        Assert.True(shown.Zip(shown.Skip(1)).All(pair => pair.Second.Picture > pair.First.Picture), "a frame was shown out of order");

        return shown;
    }

    private static void Publish(GpuPictureSlots slots, LsfgPicturePacer pacer, int group)
    {
        pacer.NextInterval(Source);

        for (var step = 0; step < Multiplier; step++)
        {
            Assert.True(slots.TryClaim(out var slot, out _), $"no free slot for group {group}'s frame {step}");
            slots.Add(new GpuPicture(0, slot, 0, 1, 1, default(VideoColour), (group * Multiplier) + step, step < Multiplier - 1));
        }

        slots.Publish();
    }

    private static void SpinFor(TimeSpan span)
    {
        var until = Stopwatch.GetTimestamp() + (long)(span.TotalSeconds * Stopwatch.Frequency);

        while (Stopwatch.GetTimestamp() < until)
        {
            Thread.SpinWait(1);
        }
    }
}
