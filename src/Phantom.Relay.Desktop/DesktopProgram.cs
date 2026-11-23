// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Immutable;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Audio.Codecs.Opus;
using Phantom.Engine.Audio.Sokol;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Hosting;
using Phantom.Engine.Logging;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Media.Vpx;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.Udp;
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Relay.App;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Pictures;
using Phantom.Relay.App.Setup;
using Phantom.Relay.App.Sound;
using Phantom.Relay.Audio;
using Phantom.Relay.Desktop.Logging;
using Phantom.Relay.Desktop.LsfgVk;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Desktop.Places;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Desktop.Setup;
using Phantom.Relay.Desktop.Stills;
using Phantom.Relay.Desktop.Updates;
using Phantom.Relay.Desktop.Windowing;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Media.Linux;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Profiles;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Network;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Desktop;

/// <summary>
/// The desktop's composition root. The window opens first and shows the setup on screen, large and centred
/// (<see cref="SetupApplication"/>; its lines go to the text console too): the kept account or a sign-in by code
/// (entered at microsoft.com/link on any device; here the page opened and the code copied, except under gamescope),
/// then the console choice (the only one without asking, else picked with the pad or the keys); the stream then takes
/// the same window over, with the H.264 decoder and the platform's pads (<see cref="DesktopWindow"/>). A setup that fails
/// (or a machine without a network or a decoder) shows why until B or Escape. F9 records the stream
/// (<see cref="StreamRecorder"/> on Windows, <see cref="PassthroughRecorder"/> on Linux, into the user's Videos
/// folder). Closing the window or Escape stops the stream and the session, after a recording still being finished is
/// done. <c>--sign-out</c> forgets the kept account first; the
/// bitrate feedback switches (<see cref="BitrateFeedbackArguments"/>: <c>--bandwidth-line</c>, <c>--remb</c>,
/// <c>--twcc</c>) choose the feedback; without them it is <see cref="BitrateFeedback.Recommended"/>, and
/// <c>--no-feedback</c> turns it off. On Windows the stream starts fullscreen (borderless,
/// <see cref="BorderlessFullscreen"/>); <c>--windowed</c> starts it as a window. A published build updates itself from
/// Nightcast's public releases at start, before the window (<see cref="SelfUpdater"/>); <c>--no-update</c> skips it.
/// <c>--external-frame-generation</c> presents only the game's new pictures from the start, for another external frame
/// generator (F12 or the menu switch it live; <see cref="StreamApplication.PresentNewPicturesOnly"/>). On Linux frame
/// generation is lsfg-vk, run inside Nightcast's own process (<see cref="LsfgVkFrameGeneration"/>): loaded before the
/// window when the player's kept choice is on; turned on the first time, Nightcast starts itself again once the window
/// closed (<see cref="ProcessRestart"/>). Tokens stay in the user's own store, never in the output.
/// </summary>
public static class DesktopProgram
{
    public const int Succeeded = 0;
    public const int StreamFailed = 1;
    public const int NoConsole = 2;
    public const int Refused = 3;

    private const string SignOutArgument = "--sign-out";
    private const string NoFeedbackArgument = "--no-feedback";
    private const string WindowedArgument = "--windowed";
    private const string NoUpdateArgument = "--no-update";
    private const string HomeVariable = "HOME";
    private const string DataHomeVariable = "XDG_DATA_HOME";

    /// <summary>What the console's start line calls a choice that is on, in lsfg-vk's state ("lsfg-vk: on").</summary>
    private const string LsfgVkStartWord = "on";

    /// <summary>Present only the game's new pictures from the start, for lsfg-vk or another external frame generator.</summary>
    private const string ExternalFrameGenerationArgument = "--external-frame-generation";

    /// <summary>Set by gamescope for the programs it runs (SteamOS's Gaming Mode): a browser opened there would cover the window.</summary>
    private const string GamescopeVariable = "GAMESCOPE_WAYLAND_DISPLAY";

    /// <summary>Large enough for any datagram WebRTC sends on a LAN path (the Ethernet MTU) with room to spare.</summary>
    private const int MaxDatagramLength = 2048;

    /// <summary>
    /// Runs it all; started by a double-click (the console window its own, its input not redirected), a run that ended
    /// in a refusal or a failure the window did not already show keeps the console open until Enter, so the reason can
    /// be read.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // A downloaded build started to finish an update replaces the old one, starts it and ends.
        if (UpdateHandoff.TryFinish(Environment.ProcessPath, args, Console.Out))
        {
            return Succeeded;
        }

        var outcome = await RunAsync(args);

        // lsfg-vk was made ready during the run: Nightcast starts again in this process, and loads it before its window.
        if (outcome.Restart && OperatingSystem.IsLinux() && Environment.ProcessPath is not null)
        {
            Console.WriteLine("Starting again to load lsfg-vk...");
            Console.WriteLine(ProcessRestart.Exec(Environment.ProcessPath, args));
        }

        if (outcome.Code != Succeeded && !outcome.ShownInWindow && !Console.IsInputRedirected)
        {
            Console.WriteLine("Press Enter to close.");
            Console.ReadLine();
        }

        return outcome.Code;
    }

    private static async Task<RunOutcome> RunAsync(string[] args)
    {
        if (!BitrateFeedbackArguments.TryParse(args, out var asked, out string? wrong))
        {
            Console.WriteLine($"{wrong}. Switches: {BitrateFeedbackArguments.Usage}, {NoFeedbackArgument}, {WindowedArgument}, {NoUpdateArgument}, {ExternalFrameGenerationArgument}");

            return new RunOutcome(Refused, false);
        }

        var feedback = ChooseFeedback(asked, args.Contains(NoFeedbackArgument));

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            cancellation.Cancel();
        };

        // A published build first takes a newer release, and starts that in its place.
        if (!args.Contains(NoUpdateArgument) && await SelfUpdater.TryUpdateAsync(Environment.ProcessPath, typeof(DesktopProgram).Assembly.GetName().Version, args, Console.Out, cancellation.Token))
        {
            return new RunOutcome(Succeeded, false);
        }

        var clock = TimeProvider.System;
        var store = UserStorage.Open(PersistentArea.ForApp(RelayApp.Id));
        using var client = new HttpClient();
        var http = new BclHttpExchange(client);
        using var auth = new XboxAuthService(http, new CredentialStore(store), clock, NullLogger<XboxAuthService>.Instance);

        if (args.Contains(SignOutArgument))
        {
            auth.SignOut();
        }

        var sessions = new XHomeSessionService(http, auth, clock, NullLogger<XHomeSessionService>.Instance);
        var profiles = BuiltInProfiles.With(new ProfileStore(store).LoadAll());
        var settings = SessionSettings.For(ProfileChoice.For(profiles, null).Stream.Resolution, clock);
        IExternalFrameGeneration? external = null;

        // On Linux lsfg-vk, before the window: its layer must be there when the window's Vulkan instance is made.
        if (OperatingSystem.IsLinux())
        {
            external = await StartLsfgVkAsync(store, cancellation.Token);
        }

        // The setup's lines go to the window's screen and the text console; the console is picked on the screen.
        var screen = new ScreenWriter(Console.Out);
        var choice = new ScreenConsoleChoice();
        var openPage = Environment.GetEnvironmentVariable(GamescopeVariable) is null;
        var setup = new TextSetup(auth, sessions, choice, screen, signIn => SignInCodeHelper.Help(signIn, screen, openPage));
        var run = new StreamRun(sessions, profiles, settings, feedback, !args.Contains(WindowedArgument), args.Contains(ExternalFrameGenerationArgument), setup, screen, choice, clock, external);
        var outcome = StreamConsole(run);

        return outcome with { Restart = external is not null && external.RestartRequested };
    }

    /// <summary>
    /// lsfg-vk in Nightcast's own folder, with the player's Lossless Scaling found in Steam's libraries and the choice
    /// kept in the user's store: loaded now when that choice is on. Null without a home folder to keep it in.
    /// </summary>
    [SupportedOSPlatform("linux")]
    private static async Task<LsfgVkFrameGeneration?> StartLsfgVkAsync(IPersistentStore store, CancellationToken cancellation)
    {
        string? home = Environment.GetEnvironmentVariable(HomeVariable);
        var folders = LsfgVkFolders.For(Environment.GetEnvironmentVariable(DataHomeVariable), home);

        if (folders is null)
        {
            return null;
        }

        var installer = new LsfgVkInstaller(folders, new HttpArchiveSource(), new XzDecompressor(), LsfgVkRelease.Sha256);
        var lossless = LsfgVkLossless.Find(LinuxSteamLibraries.Below(home));
        var lsfgVk = new LsfgVkFrameGeneration(folders, lossless, installer, new LibcEnvironment(), new VulkanLayerProbe(folders.LogPath), new LsfgVkChoiceStore(store));
        await lsfgVk.StartAsync(cancellation);

        Console.WriteLine($"Frame generation: {lsfgVk.Describe(LsfgVkStartWord)}. {LsfgVkRelease.Notice}");

        return lsfgVk;
    }

    /// <summary>
    /// The switches given, else the recommended feedback (transport-cc: about 27 Mbit/s live where none got about 11);
    /// <c>--no-feedback</c> turns all of it off.
    /// </summary>
    private static BitrateFeedback ChooseFeedback(BitrateFeedback asked, bool noFeedback)
    {
        if (noFeedback)
        {
            return BitrateFeedback.Off;
        }

        if (asked == BitrateFeedback.Off)
        {
            return BitrateFeedback.Recommended;
        }

        return asked;
    }

    /// <summary>
    /// The window from the setup to the end of the stream, then the stream, the sound and the recordings stopped. A
    /// machine without a network or a decoder shows why in the window instead.
    /// </summary>
    private static RunOutcome StreamConsole(StreamRun run)
    {
        // The log: the engine's ring, written to the console's error stream after every frame.
        using var logs = new RingLoggerFactory(LogOptions.Default, run.Clock);
        using var logFile = LogFiles.Open(run.Clock.GetLocalNow());
        var sinks = ImmutableArray.Create<ILogSink>(new TextLogSink(Console.Error));

        if (logFile is not null)
        {
            sinks = sinks.Add(new TextLogSink(logFile));
        }

        var pump = new LogPump(logs, sinks);

        uint? hostAddress = LocalAddress.Find();
        var shown = new ShownPictureTap();
        using var decoder = OpenDecoder(run.Clock, logs, shown, out string? problem);

        if (hostAddress is null || decoder is null)
        {
            ShowRefusal(run, Refusal(hostAddress, problem), pump, logs);

            return new RunOutcome(Refused, true);
        }

        return Stream(run, hostAddress.Value, decoder, shown, pump, logs);
    }

    /// <summary>The window with the reason the stream cannot start, until B or Escape.</summary>
    private static void ShowRefusal(StreamRun run, string reason, LogPump pump, ILoggerFactory logs)
    {
        using var session = new SetupSession(new RefusedSetup(reason, run.Screen), run.Screen, run.Choice);
        var log = logs.CreateLogger<SetupApplication>();

        DesktopWindow.Run(parts => new SetupApplication(parts.Device, parts.Window, parts.Fonts, session, NoStream, log), run.Fullscreen, pump);
    }

    private static IApplication NoStream(XboxConsole console)
    {
        throw new InvalidOperationException("A refused setup chooses no console.");
    }

    /// <summary>The sound, the socket and the stream made, then the window: the setup, then the stream once a console is chosen.</summary>
    private static RunOutcome Stream(StreamRun run, uint hostAddress, IVideoDecoder decoder, ShownPictureTap shown, LogPump pump, RingLoggerFactory logs)
    {
        // The sound: the console's Opus through the jitter buffer into the system's output.
        using var speakers = new SokolAudioDevice(new Logger<SokolAudioDevice>(logs));
        var player = new OpusStreamPlayer(new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels)), run.Clock, OpusStreamFormat.SampleRate, PlayoutSettings.Default);
        var sound = new AudioPacketTap(StartSound(speakers, player));
        pump.Drain();

        // A UDP socket on any address and a free port, for ICE and everything after it.
        // The doorbell is a second socket: a pad change wakes the network thread's wait through it.
        using var transport = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        using var bell = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        var doorbell = new LoopbackDoorbell(bell, transport.LocalEndpoint.Port);

        // The stream itself, on its own network thread once a console is chosen.
        var random = new SeededRandomSource((ulong)run.Clock.GetTimestamp());
        using var connection = new StreamConnection(run.Sessions, transport, doorbell, hostAddress, decoder, sound, run.Profiles, run.Clock, random, new Logger<StreamConnection>(logs));
        using var closing = new ConsoleClosing(connection);

        // The recordings and the screenshots, saved where the user keeps videos and pictures.
        var notices = new NoticeBoard(run.Clock);
        var recorder = Recorder(connection, sound, shown, run.Clock, logs);
        var screenshots = new ScreenshotTaker(connection, UserFolders.Screenshots(), notices, run.Clock, new Logger<ScreenshotTaker>(logs));

        using var session = new SetupSession(run.Setup, run.Screen, run.Choice);
        SetupApplication? setup = null;

        DesktopWindow.Run(parts => setup = Window(parts, run, session, connection, recorder, screenshots, logs), run.Fullscreen, pump);
        connection.Stop();
        speakers.Stop();
        FinishRecordings(recorder, pump);

        if (setup is null || !setup.HandedOver)
        {
            return new RunOutcome(NoConsole, true);
        }

        return new RunOutcome(Outcome(connection.Stats), false);
    }

    /// <summary>
    /// The recorder (F9): on Windows source.mp4, shown.mp4 and their comparison (Media Foundation, NVENC, ffmpeg); on
    /// Linux source.mp4 alone, the console's H.264 and Opus by the pure C# MP4 writer.
    /// </summary>
    private static IDesktopRecorder Recorder(StreamConnection connection, AudioPacketTap sound, ShownPictureTap shown, TimeProvider clock, ILoggerFactory logs)
    {
        if (OperatingSystem.IsLinux())
        {
            return new PassthroughRecorder(connection, sound, UserFolders.Videos(), clock, new Logger<PassthroughRecorder>(logs));
        }

        return new StreamRecorder(connection, shown, RecordingPlaces.Find(), clock, new Logger<StreamRecorder>(logs));
    }

    /// <summary>The window's application: the setup, handing over to the stream's on the console chosen.</summary>
    private static SetupApplication Window(WindowParts parts, StreamRun run, SetupSession session, StreamConnection connection, IDesktopRecorder recorder, ScreenshotTaker screenshots, ILoggerFactory logs)
    {
        var log = logs.CreateLogger<StreamApplication>();

        return new SetupApplication(parts.Device, parts.Window, parts.Fonts, session, console => StartStream(console, parts, run, connection, recorder, screenshots, log), log);
    }

    /// <summary>The stream starts on the console chosen; its application takes the window.</summary>
    private static StreamApplication StartStream(XboxConsole console, WindowParts parts, StreamRun run, StreamConnection connection, IDesktopRecorder recorder, ScreenshotTaker screenshots, ILogger<StreamApplication> log)
    {
        var profile = ProfileChoice.For(run.Profiles, null);
        var reports = new PadReportPolicy(PadReportSettings.Standard);
        var application = new StreamApplication(parts.Device, parts.Window, connection, new ProfilePadComposer(profile), reports, parts.Fonts, parts.Motors, recorder, screenshots, parts.Mode, run.Clock, log);

        application.PresentNewPicturesOnly(run.NewPicturesOnly);

        if (run.External is not null)
        {
            application.UseExternalGeneration(run.External);
        }

        Console.WriteLine(LosslessScalingLine());
        Console.WriteLine($"Streaming {console.Name}, asking for {run.Settings.Resolution}, bitrate feedback {run.Feedback.Describe()}. View+Menu opens the menu; Escape (or View+Menu held) ends.");
        connection.Start(console.ServerId, run.Settings, run.Feedback);

        return application;
    }

    /// <summary>Whether the user's Lossless Scaling was found through Steam, and where: LSFG and LS1 need it.</summary>
    private static string LosslessScalingLine()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "Frame generation is lsfg-vk's (the menu's Frame generation row); LS1 and NVIDIA FRUC are Windows-only. F9 records the console's video and sound (source.mp4).";
        }

        var library = Phantom.Relay.Lsfg.LosslessLocator.Find(null, Phantom.Relay.Lsfg.LosslessLocator.SteamLibraries());

        if (library is null)
        {
            return "Lossless Scaling: not found in Steam's libraries (LSFG and LS1 cannot run).";
        }

        return $"Lossless Scaling: found ({Path.GetDirectoryName(library)}); LSFG and LS1 are in the menu (View+Menu).";
    }

    /// <summary>A recording stopped as the window closed is finished (and compared) before the decoder goes.</summary>
    private static void FinishRecordings(IDesktopRecorder recorder, LogPump pump)
    {
        if (recorder.IsFinishing)
        {
            Console.WriteLine("Finishing the recording and its comparison...");
        }

        recorder.WaitUntilDone();
        pump.Flush();
    }

    /// <summary>
    /// The fastest H.264 decoder there is: on Windows Media Foundation's on the GPU (DXVA, low-latency mode: about a
    /// millisecond a 1440p picture, where openh264 took 8 on average and 41 at a keyframe), its pictures left on the GPU
    /// for the window to draw once it offers its adapter (else read back, 4.2 ms more); on Linux VA-API through FFmpeg's
    /// libavcodec; else openh264.
    /// </summary>
    private static IVideoDecoder? OpenDecoder(TimeProvider clock, ILoggerFactory logs, ShownPictureTap shown, out string? problem)
    {
        if (OperatingSystem.IsWindows())
        {
            var gpu = MediaFoundationH264Decoder.TryOpen(clock, preferHardware: true, out problem);

            if (gpu is not null)
            {
                Console.WriteLine(DecoderLine(gpu.IsHardwareAccelerated, gpu.FallbackReason));

                return Shared(gpu, clock, logs, shown);
            }
        }

        // On Linux (SteamOS) the GPU through VA-API: FFmpeg's libavcodec, its pictures read back (linux-decode.md).
        if (OperatingSystem.IsLinux())
        {
            var vaapi = VaapiH264Decoder.TryOpen(clock, null, out problem);

            if (vaapi is not null)
            {
                Console.WriteLine($"Decoding on the GPU (VA-API, {vaapi.Driver}).");

                return vaapi;
            }

            Console.WriteLine($"No VA-API decoder: {problem}");
        }

        Console.WriteLine("Decoding with openh264 in software.");

        return H264Decoder.TryOpen(out problem);
    }

    /// <summary>A decoder on the GPU shares its pictures with the window's device; one in software reads them back.</summary>
    private static IVideoDecoder Shared(MediaFoundationH264Decoder decoder, TimeProvider clock, ILoggerFactory logs, ShownPictureTap shown)
    {
        if (!decoder.IsHardwareAccelerated)
        {
            return decoder;
        }

        return new SharedPictureDecoder(decoder, clock, logs.CreateLogger<SharedPictureDecoder>(), shown);
    }

    private static string DecoderLine(bool hardware, string? fallback)
    {
        if (hardware)
        {
            return "Decoding on the GPU (Media Foundation, DXVA).";
        }

        return $"Decoding with Media Foundation in software: {fallback}";
    }

    /// <summary>The sink the stream's sound goes to: the player when the system's output opened, silence when not.</summary>
    private static IStreamAudioSink StartSound(SokolAudioDevice speakers, OpusStreamPlayer player)
    {
        if (!speakers.Open(OpusStreamPlayer.DeviceRequest))
        {
            Console.WriteLine("No sound output: the stream plays without sound.");

            return new SilentAudioSink();
        }

        speakers.Start(player);

        return new PlayerAudioSink(player);
    }

    private static string Refusal(uint? hostAddress, string? problem)
    {
        if (hostAddress is null)
        {
            return "No network with a default gateway: connect this PC to the network the console is on.";
        }

        return $"No H.264 decoder: {problem}";
    }

    private static int Outcome(StreamStats stats)
    {
        if (stats.State == StreamState.Failed)
        {
            Console.WriteLine($"The stream failed: {stats.FailureReason}");

            return StreamFailed;
        }

        Console.WriteLine("Session stopped.");

        return Succeeded;
    }
}
