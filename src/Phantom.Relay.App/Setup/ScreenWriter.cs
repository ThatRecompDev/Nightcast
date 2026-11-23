// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;

namespace Phantom.Relay.App.Setup;

/// <summary>
/// The setup's lines for the window's setup screen, passed on as they come to another writer (the text console, so
/// they stay in its scroll-back and the log). Any thread may write (the setup runs on the thread pool); the render
/// thread reads the newest lines (<see cref="Lines"/>), a line still being written last, and sees a change by
/// <see cref="Version"/>.
/// </summary>
public sealed class ScreenWriter : TextWriter
{
    private readonly TextWriter _also;
    private readonly Lock _gate = new();
    private readonly List<string> _lines = [];
    private readonly StringBuilder _current = new();
    private int _version;

    /// <summary>The lines are passed on to <paramref name="also"/> as they are written.</summary>
    public ScreenWriter(TextWriter also)
    {
        _also = also;
    }

    public override Encoding Encoding => _also.Encoding;

    /// <summary>Grows with every change, so a reader lays the lines out again only after one.</summary>
    public int Version => Volatile.Read(ref _version);

    public override void Write(char value)
    {
        lock (_gate)
        {
            Take(value);
            _version++;
        }

        _also.Write(value);
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        lock (_gate)
        {
            foreach (var character in value)
            {
                Take(character);
            }

            _version++;
        }

        _also.Write(value);
    }

    public override void WriteLine(string? value)
    {
        Write(value + NewLine);
    }

    public override void Flush()
    {
        _also.Flush();
    }

    /// <summary>The newest <paramref name="count"/> lines at most, a line still being written last.</summary>
    public string[] Lines(int count)
    {
        lock (_gate)
        {
            var all = new List<string>(_lines);

            if (_current.Length > 0)
            {
                all.Add(_current.ToString());
            }

            return [.. all.Skip(Math.Max(0, all.Count - count))];
        }
    }

    /// <summary>One character: a line break ends the line (a carriage return is dropped), anything else extends it.</summary>
    private void Take(char value)
    {
        if (value == '\n')
        {
            _lines.Add(_current.ToString());
            _current.Clear();
        }
        else if (value != '\r')
        {
            _current.Append(value);
        }
    }
}
