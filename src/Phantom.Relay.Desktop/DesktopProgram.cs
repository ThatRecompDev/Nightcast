// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Graphics.Sokol;
using Phantom.Engine.Hosting;
using Phantom.Engine.Hosting.Gamepads;
using Phantom.Engine.Hosting.Input;
using Phantom.Engine.Media.Vpx;
using Phantom.Engine.Net.Transport;
using Phantom.Engine.Net.Transport.Udp;
using Phantom.Engine.Platform.Desktop.Gamepads;
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Relay.App;
using Phantom.Relay.App.Input;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Profiles;
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

        var profiles = new ProfileStore(store).LoadAll();
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

    /// <summary>Streams the console in the window until it closes, then stops the stream.</summary>
    private static int StreamConsole(XHomeSessionService sessions, XboxConsole console, IReadOnlyList<StreamProfile> profiles, StreamSettings settings, TimeProvider clock)
    {
        uint? hostAddress = LocalAddress.Find();
        using var decoder = H264Decoder.TryOpen(out string? problem);

        if (hostAddress is null || decoder is null)
        {
            Console.WriteLine(Refusal(hostAddress, problem));

            return Refused;
        }

        // A UDP socket on any address and a free port, for ICE and everything after it.
        using var transport = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        var random = new SeededRandomSource((ulong)clock.GetTimestamp());
        using var connection = new StreamConnection(sessions, transport, hostAddress.Value, decoder, new SilentAudioSink(), profiles, clock, random, NullLogger<StreamConnection>.Instance);
        Console.WriteLine($"Streaming {console.Name}, asking for {settings.Resolution}. F3 shows the stats; Escape ends.");
        connection.Start(console.ServerId, settings);

        RunWindow(connection);
        connection.Stop();

        return Outcome(connection.Stats);
    }

    /// <summary>The window on this thread until it closes: the stream drawn, the pads read through XInput.</summary>
    private static void RunWindow(StreamConnection connection)
    {
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var application = new StreamApplication(device, window, connection, new DirectPadComposer());
        var driver = new ApplicationDriver(application, new InputFrame(InputLimits.Default), Pads());

        SokolRunner.Run(driver, new SokolWindow(WindowWidth, WindowHeight, RelayApp.Name), SokolLimits.Default);
    }

    private static IGamepadSource Pads()
    {
        if (OperatingSystem.IsWindows())
        {
            return new XInputGamepads();
        }

        return new NoGamepads();
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
