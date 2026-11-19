// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging.Abstractions;
using Phantom.Engine.Content.Storage;
using Phantom.Engine.Core.Randomness;
using Phantom.Engine.Platform.Desktop.Storage;
using Phantom.Relay.Http.Bcl;
using Phantom.Relay.Profiles;
using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Tools.Probe;

/// <summary>
/// The probe's composition root. Arguments: an optional tier (P720, P1080, P1080High, P1440; the default profile's
/// otherwise) and <c>--sign-out</c> to forget the kept account first. Tokens are kept in the user's own store
/// (<see cref="AppId"/> under the local application data), never in the repository or the output.
/// </summary>
public static class ProbeProgram
{
    public const string AppId = "phantom.relay";

    private const string SignOutArgument = "--sign-out";
    private const string Locale = "en-US";

    public static async Task<int> Main(string[] args)
    {
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
        var random = new SeededRandomSource((ulong)clock.GetTimestamp());
        var run = new ProbeRun(auth, sessions, random, Console.In, Console.Out);

        return await run.RunAsync(Settings(args, clock), cancellation.Token);
    }

    private static StreamSettings Settings(string[] args, TimeProvider clock)
    {
        var resolution = StreamProfile.Default.Stream.Resolution;

        foreach (string arg in args)
        {
            if (Enum.TryParse<StreamResolution>(arg, ignoreCase: true, out var chosen))
            {
                resolution = chosen;
            }
        }

        int offset = (int)clock.LocalTimeZone.GetUtcOffset(clock.GetUtcNow()).TotalMinutes;

        return new StreamSettings(resolution, Locale, offset);
    }
}
