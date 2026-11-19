// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;

namespace Phantom.Relay.App.Tests.Stats;

/// <summary>A logger that counts the lines written at each level.</summary>
public sealed class CountingLogger : ILogger
{
    private readonly Dictionary<LogLevel, int> _counts = [];

    public int Count(LogLevel level)
    {
        return _counts.GetValueOrDefault(level);
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _counts[logLevel] = Count(logLevel) + 1;
    }
}
