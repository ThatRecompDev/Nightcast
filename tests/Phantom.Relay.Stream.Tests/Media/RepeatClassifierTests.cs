// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Stream.Media;

namespace Phantom.Relay.Stream.Tests.Media;

/// <summary>Synthetic size sequences of the console's 60 units a second: L a new picture in motion, S a repeat's size.</summary>
public sealed class RepeatClassifierTests
{
    private const int Large = 42_000;
    private const int Small = 310;
    private const int QuietNew = 900;

    [Fact]
    public void A_30_fps_game_alternates_new_and_repeat_once_the_rhythm_held_four_pairs()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        var kinds = Classify(classifier, Alternating(16));

        Assert.All(kinds.Take(7), kind => Assert.Equal(PictureKind.New, kind));
        Assert.Equal(PictureCadence.Half, classifier.Cadence);
        Assert.Equal(Expected(count: 9, firstRepeat: true), kinds.Skip(7));
        Assert.Equal((11L, 5L), (classifier.News, classifier.Repeats));
    }

    [Fact]
    public void Gta_vi_live_whose_repeats_are_refinements_at_half_the_size_locks_to_half_rate()
    {
        // Logged from the console on 2026-11-19: GTA VI at 30 fps, the new picture then its refined repeat.
        int[] live = [22800, 43875, 22750, 44028, 22835, 45252, 22978, 43484, 21039, 42853, 23540, 42678, 19927, 41080, 19989, 40968, 21617, 43806, 13760, 41265];
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        var kinds = Classify(classifier, live);

        Assert.Equal(PictureCadence.Half, classifier.Cadence);
        Assert.All(kinds.Skip(8).Where((_, index) => index % 2 == 1), kind => Assert.Equal(PictureKind.New, kind));
        Assert.All(kinds.Skip(8).Where((_, index) => index % 2 == 0), kind => Assert.Equal(PictureKind.Repeat, kind));
    }

    [Fact]
    public void Gta_vi_live_repeats_refined_up_to_two_thirds_keep_the_rhythm()
    {
        // Logged from the console on 2026-11-19: repeats at 60 to 67 % of the picture before.
        int[] live = [22688, 37401, 22697, 37158, 22587, 36737, 22796, 36353, 22891, 38381, 22876, 38309, 25115, 38284, 25029, 37951, 24844, 37673, 24832, 37522, 24207, 36238];
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        var kinds = Classify(classifier, live);

        Assert.Equal(PictureCadence.Half, classifier.Cadence);
        Assert.All(kinds.Skip(8).Where((_, index) => index % 2 == 0), kind => Assert.Equal(PictureKind.Repeat, kind));
        Assert.Equal(7L, classifier.Repeats);
    }

    [Fact]
    public void A_60_fps_game_whose_sizes_wander_stays_at_full_rate()
    {
        int[] sizes = [40000, 31000, 42000, 30500, 39000, 33000, 41000, 29000, 38000, 32000, 43000, 30000];
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        var kinds = Classify(classifier, sizes);

        Assert.Equal(PictureCadence.Full, classifier.Cadence);
        Assert.All(kinds, kind => Assert.Equal(PictureKind.New, kind));
    }

    [Fact]
    public void A_quiet_scene_in_a_30_fps_game_keeps_its_new_pictures_new()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);
        Classify(classifier, Alternating(8));

        // Nothing moves: every unit is small, the new ones too.
        var kinds = Classify(classifier, Enumerable.Repeat(Small, 10));

        Assert.Equal(PictureCadence.Half, classifier.Cadence);
        Assert.Equal(5, kinds.Count(kind => kind == PictureKind.New));
        Assert.Equal(5, kinds.Count(kind => kind == PictureKind.Repeat));
    }

    [Fact]
    public void A_60_fps_game_is_all_new_in_motion_and_in_a_quiet_scene()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        var kinds = Classify(classifier, Enumerable.Repeat(Large, 30).Concat(Enumerable.Repeat(QuietNew, 30)));

        Assert.All(kinds, kind => Assert.Equal(PictureKind.New, kind));
        Assert.Equal(PictureCadence.Full, classifier.Cadence);
    }

    [Fact]
    public void A_game_that_goes_to_60_fps_is_followed_after_three_large_units_where_repeats_belong()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);
        Classify(classifier, Alternating(8));

        var kinds = Classify(classifier, Enumerable.Repeat(Large, 12));

        Assert.All(kinds, kind => Assert.Equal(PictureKind.New, kind));
        Assert.Equal(PictureCadence.Full, classifier.Cadence);
    }

    [Fact]
    public void A_slip_of_the_rhythm_moves_the_phase_on_by_one()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);
        Classify(classifier, Alternating(8));

        // The console sent one picture late: two new pictures in a row, then the rhythm one unit later.
        var kinds = Classify(classifier, [Large, Large, Small, Large, Small, Large, Small]);

        Assert.Equal([PictureKind.New, PictureKind.New, PictureKind.Repeat, PictureKind.New, PictureKind.Repeat, PictureKind.New, PictureKind.Repeat], kinds);
        Assert.Equal(PictureCadence.Half, classifier.Cadence);
    }

    [Fact]
    public void A_key_frame_is_new_whatever_its_size()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard);

        Assert.Equal(PictureKind.New, classifier.Classify(Small, key: true));
    }

    [Fact]
    public void The_threshold_is_a_setting()
    {
        var classifier = new RepeatClassifier(RepeatSettings.Standard with { RepeatBytes = QuietNew });

        var kinds = Classify(classifier, Enumerable.Range(0, 8).Select(index => SizeAt(index, QuietNew)));

        Assert.Equal(PictureKind.Repeat, kinds[^1]);
    }

    // L S L S ...: a 30 fps game in motion.
    private static IEnumerable<int> Alternating(int count)
    {
        return Enumerable.Range(0, count).Select(index => SizeAt(index, Small));
    }

    private static List<PictureKind> Expected(int count, bool firstRepeat)
    {
        return Enumerable.Range(0, count).Select(index => KindAt(index, firstRepeat)).ToList();
    }

    // Even units large, odd ones the given small size.
    private static int SizeAt(int index, int small)
    {
        if (index % 2 == 0)
        {
            return Large;
        }

        return small;
    }

    private static PictureKind KindAt(int index, bool firstRepeat)
    {
        if ((index % 2 == 0) == firstRepeat)
        {
            return PictureKind.Repeat;
        }

        return PictureKind.New;
    }

    private static List<PictureKind> Classify(RepeatClassifier classifier, IEnumerable<int> sizes)
    {
        return sizes.Select(size => classifier.Classify(size, key: false)).ToList();
    }
}
