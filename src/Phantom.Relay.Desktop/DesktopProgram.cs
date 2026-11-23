// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Collections.Immutable;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Audio.Codecs.Opus;
using Phantom.Engine.Audio.Sokol;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Graphics.Sokol;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Logging;
using Phantom.Engine.Media.Video;
using Phantom.Engine.Media.Vpx;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.Udp;
using Phantom.Engine.Platform.Desktop.Fonts;
using Phantom.Engine.Platform.Desktop.Gamepads;
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Engine.Ui.Text;
using Phantom.Relay.App;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Recording;
using Phantom.Relay.App.Sound;
using Phantom.Relay.Audio;
using Phantom.Relay.Desktop.Fonts;
using Phantom.Relay.Desktop.Logging;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Desktop.Recording;
using Phantom.Relay.Desktop.Setup;
using Phantom.Relay.Desktop.Updates;
using Phantom.Relay.Desktop.Windowing;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Media.Linux;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Pads.Linux;
using Phantom.Relay.Pads.Windows;
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
/// The desktop's composition root: sign-in on the console window (the kept account, or a code entered at
/// microsoft.com/link on any device, the page opened and the code copied here), the console choice, then the stream in a resizable window with the H.264 decoder and the XInput pads.
/// F9 records the stream (<see cref="StreamRecorder"/>, into the user's Videos folder). Closing the window or Escape
/// stops the stream and the session, after a recording still being finished is done. <c>--sign-out</c> forgets the kept account first;
/// the bitrate feedback switches (<see cref="BitrateFeedbackArguments"/>: <c>--bandwidth-line</c>, <c>--remb</c>,
/// <c>--twcc</c>) choose the feedback; without them it is <see cref="BitrateFeedback.Recommended"/>, and
/// <c>--no-feedback</c> turns it off. The window starts fullscreen (borderless, <see cref="BorderlessFullscreen"/>);
/// <c>--windowed</c> starts it as a window. With one console it connects without asking. A published build updates
/// itself from Nightcast's public releases at start (<see cref="SelfUpdater"/>); <c>--no-update</c> skips it.
/// Tokens stay in the user's own store, never in the output.
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
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    /// <summary>Large enough for any datagram WebRTC sends on a LAN path (the Ethernet MTU) with room to spare.</summary>
    private const int MaxDatagramLength = 2048;

    /// <summary>
    /// Runs it all; started by a double-click (the console window its own, its input not redirected), a run that ended
    /// in a refusal or a failure keeps the window open until Enter, so the reason can be read.
    /// </summary>
    public static async Task<int> Main(string[] args)
    {
        // A downloaded build started to finish an update replaces the old one, starts it and ends.
        if (UpdateHandoff.TryFinish(Environment.ProcessPath, args, Console.Out))
        {
            return Succeeded;
        }

        int outcome = await RunAsync(args);

        if (outcome != Succeeded && !Console.IsInputRedirected)
        {
            Console.WriteLine("Press Enter to close.");
            Console.ReadLine();
        }

        return outcome;
    }

    private static async Task<int> RunAsync(string[] args)
    {
        if (!BitrateFeedbackArguments.TryParse(args, out var asked, out string? wrong))
        {
            Console.WriteLine($"{wrong}. Switches: {BitrateFeedbackArguments.Usage}, {NoFeedbackArgument}, {WindowedArgument}, {NoUpdateArgument}");

            return Refused;
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
            return Succeeded;
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
        var console = await ChooseConsoleAsync(new TextSetup(auth, sessions, Console.In, Console.Out, signIn => SignInCodeHelper.Help(signIn, Console.Out)), cancellation.Token);

        if (console is null)
        {
            return NoConsole;
        }

        var profiles = BuiltInProfiles.With(new ProfileStore(store).LoadAll());
        var settings = SessionSettings.For(ProfileChoice.For(profiles, null).Stream.Resolution, clock);

        bool fullscreen = !args.Contains(WindowedArgument);

        return StreamConsole(sessions, console, profiles, settings, feedback, fullscreen, clock);
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

    /// <summary>Signs in and picks the console; null when there is none or the service refused.</summary>
    private static async Task<XboxConsole?> ChooseConsoleAsync(TextSetup setup, CancellationToken cancellation)
    {
        try
        {
            await setup.SignInAsync(cancellation);

            return await setup.ChooseConsoleAsync(cancellation);
        }
        catch (XboxAuthException error)
        {
            Console.WriteLine($"Sign-in refused: {error.Message}");

            return null;
        }
        catch (XHomeSessionException error)
        {
            Console.WriteLine($"Streaming service refused: {error.Message}");

            return null;
        }
    }

    /// <summary>Streams the console in the window until it closes, then stops the stream, the sound and the pads.</summary>
    private static int StreamConsole(XHomeSessionService sessions, XboxConsole console, IReadOnlyList<StreamProfile> profiles, StreamSettings settings, BitrateFeedback feedback, bool fullscreen, TimeProvider clock)
    {
        // The log: the engine's ring, written to the console's error stream after every frame.
        using var logs = new RingLoggerFactory(LogOptions.Default, clock);
        using var logFile = LogFiles.Open(clock.GetLocalNow());
        var sinks = ImmutableArray.Create<ILogSink>(new TextLogSink(Console.Error));

        if (logFile is not null)
        {
            sinks = sinks.Add(new TextLogSink(logFile));
        }

        var pump = new LogPump(logs, sinks);

        uint? hostAddress = LocalAddress.Find();
        var shown = new ShownPictureTap();
        using var decoder = OpenDecoder(clock, logs, shown, out string? problem);

        if (hostAddress is null || decoder is null)
        {
            Console.WriteLine(Refusal(hostAddress, problem));

            return Refused;
        }

        // The sound: the console's Opus through the jitter buffer into the system's output.
        using var speakers = new SokolAudioDevice(new Logger<SokolAudioDevice>(logs));
        var player = new OpusStreamPlayer(new EngineOpusFrameDecoder(new OpusDecoder(OpusStreamFormat.Channels)), clock, OpusStreamFormat.SampleRate, PlayoutSettings.Default);
        IStreamAudioSink sound = StartSound(speakers, player);
        pump.Drain();

        // A UDP socket on any address and a free port, for ICE and everything after it.
        // The doorbell is a second socket: a pad change wakes the network thread's wait through it.
        using var transport = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        using var bell = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        var doorbell = new LoopbackDoorbell(bell, transport.LocalEndpoint.Port);

        // The stream itself, on its own network thread.
        var random = new SeededRandomSource((ulong)clock.GetTimestamp());
        using var connection = new StreamConnection(sessions, transport, doorbell, hostAddress.Value, decoder, sound, profiles, clock, random, new Logger<StreamConnection>(logs));
        using var closing = new ConsoleClosing(connection);
        var recorder = new StreamRecorder(connection, shown, RecordingPlaces.Find(), clock, new Logger<StreamRecorder>(logs));

        Console.WriteLine(LosslessScalingLine());
        Console.WriteLine($"Streaming {console.Name}, asking for {settings.Resolution}, bitrate feedback {feedback.Describe()}. View+Menu opens the menu; Escape (or View+Menu held) ends.");
        connection.Start(console.ServerId, settings, feedback);

        RunWindow(connection, recorder, ProfileChoice.For(profiles, null), fullscreen, pump, clock, new Logger<StreamApplication>(logs));
        connection.Stop();
        speakers.Stop();
        FinishRecordings(recorder, pump);

        return Outcome(connection.Stats);
    }

    /// <summary>Whether the user's Lossless Scaling was found through Steam, and where: LSFG and LS1 need it.</summary>
    private static string LosslessScalingLine()
    {
        var library = Phantom.Relay.Lsfg.LosslessLocator.Find(null, Phantom.Relay.Lsfg.LosslessLocator.SteamLibraries());

        if (library is null)
        {
            return "Lossless Scaling: not found in Steam's libraries (LSFG and LS1 cannot run).";
        }

        return $"Lossless Scaling: found ({Path.GetDirectoryName(library)}); LSFG and LS1 are in the menu (View+Menu).";
    }

    /// <summary>A recording stopped as the window closed is finished (and compared) before the decoder goes.</summary>
    private static void FinishRecordings(StreamRecorder recorder, LogPump pump)
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

    /// <summary>
    /// The window on this thread until it closes: the stream drawn, the pads (on Windows Xbox pads through XInput and
    /// PlayStation pads through HID, as one; on Linux every pad through evdev) and keyboard and mouse composed under the
    /// profile and sent the moment they change.
    /// </summary>
    private static void RunWindow(StreamConnection connection, IStreamRecorder recorder, StreamProfile profile, bool fullscreen, LogPump pump, TimeProvider clock, ILogger<StreamApplication> log)
    {
        if (OperatingSystem.IsWindows())
        {
            RunWindowWithWindowsPads(connection, recorder, profile, fullscreen, pump, clock, log);

            return;
        }

        if (OperatingSystem.IsLinux())
        {
            RunWindowWithLinuxPads(connection, recorder, profile, pump, clock, log);

            return;
        }

        Run(connection, recorder, profile, new NoGamepads(), new NoSystemFonts(), new WindowedOnly(), pump, clock, log);
    }

    /// <summary>
    /// Linux's pads through evdev (on SteamOS Steam Input's virtual pad), leaving alone the pads Steam lists as its
    /// own; the window as it opens (gamescope shows it fullscreen in Gaming Mode).
    /// </summary>
    [SupportedOSPlatform("linux")]
    private static void RunWindowWithLinuxPads(StreamConnection connection, IStreamRecorder recorder, StreamProfile profile, LogPump pump, TimeProvider clock, ILogger<StreamApplication> log)
    {
        using var pads = new EvdevPads(Environment.GetEnvironmentVariable(EvdevPads.IgnoreVariable));
        pads.Start();
        Run(connection, recorder, profile, pads, LinuxFonts(), new WindowedOnly(), pump, clock, log);
        pads.Stop();
    }

    /// <summary>The fonts under fontconfig's folders, the user's own first.</summary>
    private static LinuxSystemFonts LinuxFonts()
    {
        return new LinuxSystemFonts(LinuxSystemFonts.Folders(Environment.GetEnvironmentVariable(HomeVariable), Environment.GetEnvironmentVariable(DataHomeVariable)));
    }

    /// <summary>Windows' pads and fonts, and its borderless fullscreen, started in it when <paramref name="fullscreen"/>.</summary>
    [SupportedOSPlatform("windows")]
    private static void RunWindowWithWindowsPads(StreamConnection connection, IStreamRecorder recorder, StreamProfile profile, bool fullscreen, LogPump pump, TimeProvider clock, ILogger<StreamApplication> log)
    {
        using var playStation = new PlayStationPads();
        playStation.Start();

        var xbox = new GamepadsWithFeedback(new XInputGamepads(), new XInputMotors());
        var pads = new CombinedGamepads(xbox, playStation, InputLimits.DefaultGamepads);
        Run(connection, recorder, profile, pads, new WindowsSystemFonts(), new BorderlessFullscreen(fullscreen), pump, clock, log);
        playStation.Stop();
    }

    /// <summary>The window and its frames until it closes, with <paramref name="pads"/> read once a frame and the stats drawn in a font of <paramref name="fonts"/>.</summary>
    private static void Run(StreamConnection connection, IStreamRecorder recorder, StreamProfile profile, IGamepadSource pads, ISystemFonts fonts, IWindowMode mode, LogPump pump, TimeProvider clock, ILogger<StreamApplication> log)
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var reports = new PadReportPolicy(PadReportSettings.Standard);
        var application = new StreamApplication(device, window, connection, new ProfilePadComposer(profile), reports, fonts, pads as IGamepadFeedback, recorder, mode, clock, log);
        var driver = new ApplicationDriver(new LogDrainingApplication(application, pump), new InputFrame(InputLimits.Default), pads);

        SokolRunner.Run(driver, new SokolWindow(WindowWidth, WindowHeight, RelayApp.Name), SokolLimits.Default);
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
