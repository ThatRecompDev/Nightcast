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
using Phantom.Engine.Platform.Desktop.Gamepads;
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Relay.App;
using Phantom.Relay.App.Input;
using Phantom.Relay.App.Sound;
using Phantom.Relay.Audio;
using Phantom.Relay.Desktop.Pictures;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Media.Windows;
using Phantom.Relay.Pads.Windows;
using Phantom.Relay.Profiles;
using Phantom.Relay.Profiles.Input;
using Phantom.Relay.Stream;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Network;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Desktop;

/// <summary>
/// The desktop's composition root: sign-in on the console window (the kept account, or the sign-in page and the pasted
/// address), the console choice, then the stream in a resizable window with the H.264 decoder and the XInput pads.
/// Closing the window or Escape stops the stream and the session. <c>--sign-out</c> forgets the kept account first.
/// Tokens stay in the user's own store, never in the output.
/// </summary>
public static class DesktopProgram
{
    public const int Succeeded = 0;
    public const int StreamFailed = 1;
    public const int NoConsole = 2;
    public const int Refused = 3;

    private const string SignOutArgument = "--sign-out";
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    /// <summary>Large enough for any datagram WebRTC sends on a LAN path (the Ethernet MTU) with room to spare.</summary>
    private const int MaxDatagramLength = 2048;

    public static async Task<int> Main(string[] args)
    {
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            cancellation.Cancel();
        };

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
        var console = await ChooseConsoleAsync(new TextSetup(auth, sessions, Console.In, Console.Out), cancellation.Token);

        if (console is null)
        {
            return NoConsole;
        }

        var profiles = BuiltInProfiles.With(new ProfileStore(store).LoadAll());
        var settings = SessionSettings.For(ProfileChoice.For(profiles, null).Stream.Resolution, clock);

        return StreamConsole(sessions, console, profiles, settings, clock);
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
    private static int StreamConsole(XHomeSessionService sessions, XboxConsole console, IReadOnlyList<StreamProfile> profiles, StreamSettings settings, TimeProvider clock)
    {
        // The log: the engine's ring, written to the console's error stream after every frame.
        using var logs = new RingLoggerFactory(LogOptions.Default, clock);
        var pump = new LogPump(logs, ImmutableArray.Create<ILogSink>(new TextLogSink(Console.Error)));

        uint? hostAddress = LocalAddress.Find();
        using var decoder = OpenDecoder(clock, logs, out string? problem);

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
        Console.WriteLine($"Streaming {console.Name}, asking for {settings.Resolution}. F3 shows the stats; Escape ends.");
        connection.Start(console.ServerId, settings);

        RunWindow(connection, ProfileChoice.For(profiles, null), pump, clock);
        connection.Stop();
        speakers.Stop();
        pump.Flush();

        return Outcome(connection.Stats);
    }

    /// <summary>
    /// The fastest H.264 decoder there is: on Windows Media Foundation's on the GPU (DXVA, low-latency mode: about a
    /// millisecond a 1440p picture, where openh264 took 8 on average and 41 at a keyframe), its pictures left on the GPU
    /// for the window to draw once it offers its adapter (else read back, 4.2 ms more), else openh264.
    /// </summary>
    private static IVideoDecoder? OpenDecoder(TimeProvider clock, ILoggerFactory logs, out string? problem)
    {
        if (OperatingSystem.IsWindows())
        {
            var gpu = MediaFoundationH264Decoder.TryOpen(clock, preferHardware: true, out problem);

            if (gpu is not null)
            {
                Console.WriteLine(DecoderLine(gpu.IsHardwareAccelerated, gpu.FallbackReason));

                return Shared(gpu, clock, logs);
            }
        }

        Console.WriteLine("Decoding with openh264 in software.");

        return H264Decoder.TryOpen(out problem);
    }

    /// <summary>A decoder on the GPU shares its pictures with the window's device; one in software reads them back.</summary>
    private static IVideoDecoder Shared(MediaFoundationH264Decoder decoder, TimeProvider clock, ILoggerFactory logs)
    {
        if (!decoder.IsHardwareAccelerated)
        {
            return decoder;
        }

        return new SharedPictureDecoder(decoder, clock, logs.CreateLogger<SharedPictureDecoder>());
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
    /// The window on this thread until it closes: the stream drawn, the pads (Xbox pads through XInput, PlayStation pads
    /// through HID, as one) and keyboard and mouse composed under the profile and sent the moment they change.
    /// </summary>
    private static void RunWindow(StreamConnection connection, StreamProfile profile, LogPump pump, TimeProvider clock)
    {
        if (OperatingSystem.IsWindows())
        {
            RunWindowWithWindowsPads(connection, profile, pump, clock);

            return;
        }

        Run(connection, profile, new NoGamepads(), pump, clock);
    }

    [SupportedOSPlatform("windows")]
    private static void RunWindowWithWindowsPads(StreamConnection connection, StreamProfile profile, LogPump pump, TimeProvider clock)
    {
        using var playStation = new PlayStationPads();
        playStation.Start();

        Run(connection, profile, new CombinedGamepads(new XInputGamepads(), playStation, InputLimits.DefaultGamepads), pump, clock);
        playStation.Stop();
    }

    /// <summary>The window and its frames until it closes, with <paramref name="pads"/> read once a frame.</summary>
    private static void Run(StreamConnection connection, StreamProfile profile, IGamepadSource pads, LogPump pump, TimeProvider clock)
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var reports = new PadReportPolicy(PadReportSettings.Standard);
        var application = new StreamApplication(device, window, connection, new ProfilePadComposer(profile), reports, clock);
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
