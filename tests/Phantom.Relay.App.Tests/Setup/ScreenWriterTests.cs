// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.App.Setup;

namespace Phantom.Relay.App.Tests.Setup;

public sealed class ScreenWriterTests : IDisposable
{
    private readonly StringWriter _console = new();

    public void Dispose()
    {
        _console.Dispose();
    }

    [Fact]
    public void Lines_are_kept_for_the_screen_and_passed_on_as_they_come()
    {
        var screen = new ScreenWriter(_console);

        screen.WriteLine("Sign in.");
        screen.Write("Console number > ");

        Assert.Equal(["Sign in.", "Console number > "], screen.Lines(10));
        Assert.Equal("Sign in." + Environment.NewLine + "Console number > ", _console.ToString());
    }

    [Fact]
    public void Only_the_newest_lines_are_shown_and_carriage_returns_are_dropped()
    {
        var screen = new ScreenWriter(_console);

        screen.Write("one\r\ntwo\nthree\n");
        screen.WriteLine();

        Assert.Equal(["three", string.Empty], screen.Lines(2));
    }

    [Fact]
    public void Every_write_grows_the_version()
    {
        var screen = new ScreenWriter(_console);
        var before = screen.Version;

        screen.Write('a');
        var middle = screen.Version;
        screen.Write(string.Empty);

        Assert.True(middle > before);
        Assert.Equal(middle, screen.Version);
    }
}
