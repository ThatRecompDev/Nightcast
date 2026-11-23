// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Session;

namespace Phantom.Relay.Stream.Setup;

/// <summary>The console picked by its number (from 1) typed on a text console.</summary>
public sealed class TextConsoleChoice : IConsoleChoice
{
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public TextConsoleChoice(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public async Task<int?> ChooseAsync(IReadOnlyList<XboxConsole> consoles, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(consoles);

        _output.Write("Console number > ");
        string? choice = await _input.ReadLineAsync(cancellation);

        if (!int.TryParse(choice, out int number) || number < 1 || number > consoles.Count)
        {
            return null;
        }

        return number - 1;
    }
}
