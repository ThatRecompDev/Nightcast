// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Engine.Media.Video;
using Phantom.Relay.App.Recording;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Stills;

namespace Phantom.Relay.Desktop.Stills;

/// <summary>
/// The desktop's screenshots (F1, the menu; docs/architecture/screenshots.md), on Windows and Linux alike.
/// <see cref="Take"/> asks the decode thread for its next picture (<see cref="Stream.Media.PictureGrab"/>: the console's
/// own, at the stream's size, read back from the GPU where it stays there) and returns; on a worker it is converted to
/// RGB and written as a PNG (<see cref="StillImage"/>) under a partial name, renamed to its own
/// (<see cref="ScreenshotFile"/>) once whole, so a program watching the folder never sees half a file.
/// <see cref="Status"/> is the notice board's line.
/// </summary>
internal sealed class ScreenshotTaker : IScreenshots
{
    /// <summary>NOT DATA: how long a screenshot waits for the next decoded picture (a still game sends few).</summary>
    public static readonly TimeSpan PictureWait = TimeSpan.FromSeconds(3);

    private const string NothingYet = "no screenshot: no picture was decoded yet";
    private const string NoPicture = "no screenshot: no new picture came";
    private const string Taking = "taking a screenshot";

    private readonly StreamConnection _connection;
    private readonly string _root;
    private readonly NoticeBoard _notices;
    private readonly TimeProvider _clock;
    private readonly ILogger _log;

    /// <summary>Screenshots of <paramref name="connection"/> go under <paramref name="root"/> (<see cref="Places.UserFolders.Screenshots"/>).</summary>
    public ScreenshotTaker(StreamConnection connection, string root, NoticeBoard notices, TimeProvider clock, ILogger<ScreenshotTaker> log)
    {
        _connection = connection;
        _root = root;
        _notices = notices;
        _clock = clock;
        _log = log;
    }

    public string? Status => _notices.Current;

    public void Take()
    {
        if (_connection.Stats.Width == 0)
        {
            Refuse(NothingYet);

            return;
        }

        var request = _connection.Stills.Request();

        if (request is null)
        {
            return;
        }

        _notices.Post(Taking);
        _ = Task.Run(() => SaveAsync(request));
    }

    private async Task SaveAsync(Task<VideoFrame> request)
    {
        VideoFrame picture;

        try
        {
            picture = await request.WaitAsync(PictureWait).ConfigureAwait(false);
        }
        catch (Exception error) when (error is TimeoutException or OperationCanceledException)
        {
            _connection.Stills.Cancel();
            Refuse(NoPicture);

            return;
        }

        Save(picture);
    }

    // The PNG written under its partial name, then renamed to its own.
    private void Save(VideoFrame picture)
    {
        try
        {
            var path = ScreenshotFile.For(_root, _clock.GetLocalNow(), File.Exists);
            var partial = path + ScreenshotFile.PartialSuffix;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using (var file = new FileStream(partial, FileMode.Create, FileAccess.Write))
            {
                StillImage.WritePng(picture, file);
            }

            File.Move(partial, path);
            Saved(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Refuse(error.Message);
        }
    }

    private void Saved(string path)
    {
        _notices.Post($"Screenshot saved: {path}");

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.ScreenshotSaved(ScreenshotFile.Short(path));
        }
    }

    private void Refuse(string reason)
    {
        _notices.Post(reason);
        _log.ScreenshotFailed(reason);
    }
}
