// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Globalization;
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
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Profiles;
using Phantom.Relay.Stream.Feedback;
using Phantom.Relay.Stream.Media;
using Phantom.Relay.Stream.Network;
using Phantom.Relay.Stream.Setup;
using Phantom.Relay.WebRtc.Dtls;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The probe's composition root. Arguments: an optional tier (P720, P1080, P1080High, P1440; the default profile's
/// otherwise), <c>--view</c> to watch the stream in a window until it closes, <c>--seconds &lt;n&gt;</c> to measure for
/// n seconds instead of six, the bitrate feedback switches (<see cref="BitrateFeedbackArguments"/>), and
/// <c>--sign-out</c> to forget the kept account first. Tokens are kept in the user's own store (<see cref="AppId"/> under the local application data), never
/// in the repository or the output.
/// </summary>
public static class ProbeProgram
{
    public const string AppId = RelayApp.Id;

    private const string SignOutArgument = "--sign-out";
    private const string ViewArgument = "--view";
    private const string SecondsArgument = "--seconds";
    private const int WindowWidth = 1280;
    private const int WindowHeight = 720;

    /// <summary>Large enough for any datagram WebRTC sends on a LAN path (the Ethernet MTU) with room to spare.</summary>
    private const int MaxDatagramLength = 2048;

    /// <summary>How long a measuring run keeps the stream: enough for the channels, the first frames and a keep-alive.</summary>
    private static readonly TimeSpan MeasureFor = TimeSpan.FromSeconds(6);

    public static async Task<int> Main(string[] args)
    {
        // A wrong switch leaves its error in wrong, which refuses the run below.
        _ = BitrateFeedbackArguments.TryParse(args, out var feedback, out string? wrong);
        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            cancellation.Cancel();
        };

        var clock = TimeProvider.System;
        using var client = new HttpClient();
        var http = new BclHttpExchange(client);
        var credentials = new CredentialStore(UserStorage.Open(PersistentArea.ForApp(AppId)));
        using var auth = new XboxAuthService(http, credentials, clock, NullLogger<XboxAuthService>.Instance);

        if (args.Contains(SignOutArgument))
        {
            auth.SignOut();
        }

        var sessions = new XHomeSessionService(http, auth, clock, NullLogger<XHomeSessionService>.Instance);
        uint? hostAddress = LocalAddress.Find();
        using var decoder = H264Decoder.TryOpen(out string? problem);

        string? refusal = Refusal(wrong, hostAddress, decoder, problem);

        if (refusal is not null || hostAddress is null || decoder is null)
        {
            Console.WriteLine(refusal);

            return ProbeRun.Refused;
        }

        // The decode worker, and a UDP socket on any address and a free port, for ICE and everything after it.
        bool view = args.Contains(ViewArgument);
        var listen = view ? TimeSpan.MaxValue : ListenFor(args);
        var mailbox = new PictureMailbox();
        using var video = new DecodeWorker(decoder, mailbox, clock);
        video.Start();

        var random = new SeededRandomSource((ulong)clock.GetTimestamp());
        using var transport = new UdpDatagramTransport(DatagramEndpoint.FromOctets(0, 0, 0, 0, 0), MaxDatagramLength);
        var ice = new LiveIce(sessions, transport, hostAddress.Value, video, clock, random, listen, feedback, Console.Out);

        // The session's certificate and the run.
        var certificate = DtlsCertificate.Create(new Org.BouncyCastle.Security.SecureRandom(), clock.GetUtcNow());
        var run = new ProbeRun(auth, sessions, ice, certificate, random, Console.In, Console.Out);

        if (!view)
        {
            return await run.RunAsync(Settings(args, clock), feedback, cancellation.Token);
        }

        return await ViewAsync(run, Settings(args, clock), feedback, mailbox, cancellation);
    }

    /// <summary>
    /// The stream runs on a background task while the window runs on this thread until it closes; closing it cancels
    /// the stream, whose session is then stopped.
    /// </summary>
    private static async Task<int> ViewAsync(ProbeRun run, StreamSettings settings, BitrateFeedback feedback, PictureMailbox mailbox, CancellationTokenSource cancellation)
    {
        var stream = Task.Run(() => run.RunAsync(settings, feedback, cancellation.Token));
        var window = new SokolWindowHost();
        var device = new SokolDevice(SokolLimits.Default, NullLogger<SokolDevice>.Instance);
        var application = new ViewerApplication(device, window, mailbox);
        var driver = new ApplicationDriver(application, new InputFrame(InputLimits.Default), new NoGamepads());
        SokolRunner.Run(driver, new SokolWindow(WindowWidth, WindowHeight, RelayApp.Name), SokolLimits.Default);

        cancellation.Cancel();

        try
        {
            return await stream;
        }
        catch (OperationCanceledException)
        {
            return ProbeRun.Succeeded;
        }
    }

    /// <summary>
    /// Why the probe cannot run: a wrong bitrate feedback switch, no network to the console, or no H.264 decoder; null
    /// when it can.
    /// </summary>
    private static string? Refusal(string? wrongSwitch, uint? hostAddress, H264Decoder? decoder, string? problem)
    {
        if (wrongSwitch is not null)
        {
            return $"{wrongSwitch}. Switches: {BitrateFeedbackArguments.Usage}";
        }

        if (hostAddress is null)
        {
            return "No network with a default gateway: connect this PC to the network the console is on.";
        }

        if (decoder is null)
        {
            return $"No H.264 decoder: {problem}";
        }

        return null;
    }

    private static StreamSettings Settings(string[] args, TimeProvider clock)
    {
        var resolution = StreamProfile.Default.Stream.Resolution;

        foreach (string arg in args)
        {
            // A number (a switch's value) parses as any enum value; only a tier's name is taken.
            if (Enum.TryParse<StreamResolution>(arg, ignoreCase: true, out var chosen) && Enum.IsDefined(chosen) && !char.IsAsciiDigit(arg[0]))
            {
                resolution = chosen;
            }
        }

        return SessionSettings.For(resolution, clock);
    }

    /// <summary>How long a measuring run streams: <c>--seconds &lt;n&gt;</c>, else <see cref="MeasureFor"/>.</summary>
    private static TimeSpan ListenFor(string[] args)
    {
        int at = Array.IndexOf(args, SecondsArgument);

        if (at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return MeasureFor;
    }
}
