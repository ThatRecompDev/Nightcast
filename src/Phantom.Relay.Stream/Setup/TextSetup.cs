// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth;
using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>
/// Sign-in and the console choice on a text console, until the app has its own screens. Sign-in is by code: a short
/// code is shown, the player enters it at the page shown (microsoft.com/link) on any phone or computer, and the setup
/// waits; the head may open the page and copy the code for the player (<c>onCode</c>). A kept sign-in the service no
/// longer accepts starts a new one. The consoles are listed and one is picked by number (the only one without asking).
/// It never writes a token.
/// </summary>
public sealed class TextSetup
{
    private readonly IXboxAuthService _auth;
    private readonly IXHomeSessionService _sessions;
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly Action<DeviceSignIn>? _onCode;

    /// <summary><paramref name="onCode"/> sees each new sign-in code as it is shown (to open the page, copy the code); null for none.</summary>
    public TextSetup(IXboxAuthService auth, IXHomeSessionService sessions, TextReader input, TextWriter output, Action<DeviceSignIn>? onCode)
    {
        _auth = auth;
        _sessions = sessions;
        _input = input;
        _output = output;
        _onCode = onCode;
    }

    /// <summary>Signs in with the kept account, or by code when none is kept or the kept one expired.</summary>
    public async Task SignInAsync(CancellationToken cancellation)
    {
        if (!_auth.IsSignedIn)
        {
            await SignInByCodeAsync(cancellation);
        }

        StreamingToken token;

        try
        {
            token = await _auth.GetStreamingTokenAsync(cancellation);
        }
        catch (XboxAuthException expired) when (expired.SignInAgain)
        {
            _output.WriteLine(expired.Message);
            await SignInByCodeAsync(cancellation);
            token = await _auth.GetStreamingTokenAsync(cancellation);
        }

        _output.WriteLine($"Signed in. Streaming region: {token.DefaultRegion.Name} ({token.Regions.Count} offered)");
    }

    private async Task SignInByCodeAsync(CancellationToken cancellation)
    {
        var signIn = await _auth.StartDeviceSignInAsync(cancellation);

        _output.WriteLine();
        _output.WriteLine("Sign in with the Microsoft account that owns the console.");
        _output.WriteLine($"On any phone or computer, open  {signIn.Page}");
        _output.WriteLine($"and enter the code              {signIn.UserCode}");
        _output.WriteLine();
        _onCode?.Invoke(signIn);

        _output.WriteLine("Waiting for the sign-in...");
        await _auth.CompleteDeviceSignInAsync(signIn, cancellation);
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
