// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>
/// Sign-in and the console choice on a text console, until the app has its own screens: the sign-in page's address is
/// shown and the address the page ends on is pasted back; the consoles are listed and one is picked by number (the only
/// one without asking). It never writes a token.
/// </summary>
public sealed class TextSetup
{
    private readonly IXboxAuthService _auth;
    private readonly IXHomeSessionService _sessions;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public TextSetup(IXboxAuthService auth, IXHomeSessionService sessions, TextReader input, TextWriter output)
    {
        _auth = auth;
        _sessions = sessions;
        _input = input;
        _output = output;
    }

    /// <summary>Signs in with the kept account, or through the pasted address when none is kept.</summary>
    public async Task SignInAsync(CancellationToken cancellation)
    {
        if (!_auth.IsSignedIn)
        {
            _output.WriteLine("Open this page, sign in with the account that owns the console, then paste the address the page ends on:");
            _output.WriteLine(_auth.SignInAddress);
            _output.Write("> ");
            string? pasted = await _input.ReadLineAsync(cancellation);

            if (!Uri.TryCreate(pasted?.Trim(), UriKind.Absolute, out var redirected))
            {
                throw new XboxAuthException("That was not an address.");
            }

            await _auth.CompleteSignInAsync(redirected, cancellation);
        }

        var token = await _auth.GetStreamingTokenAsync(cancellation);
        _output.WriteLine($"Signed in. Streaming region: {token.DefaultRegion.Name} ({token.Regions.Count} offered)");
    }

    /// <summary>The console to stream from, or null when there is none or the choice was not one.</summary>
    public async Task<XboxConsole?> ChooseConsoleAsync(CancellationToken cancellation)
    {
        var consoles = await _sessions.GetConsolesAsync(cancellation);

        if (consoles.Count == 0)
        {
            _output.WriteLine("No console found. Turn on remote features on the console (Settings, Devices & connections, Remote features).");

            return null;
        }

        for (int i = 0; i < consoles.Count; i++)
        {
            var console = consoles[i];
            string streaming = console.IsStreamingEnabled ? "on" : "off";
            _output.WriteLine($"  {i + 1}. {console.Name} ({console.ConsoleType}, {console.PowerState}, streaming {streaming})");
        }

        if (consoles.Count == 1)
        {
            return consoles[0];
        }

        _output.Write("Console number > ");
        string? choice = await _input.ReadLineAsync(cancellation);

        if (!int.TryParse(choice, out int number) || number < 1 || number > consoles.Count)
        {
            _output.WriteLine("No such console.");

            return null;
        }

        return consoles[number - 1];
    }
}
