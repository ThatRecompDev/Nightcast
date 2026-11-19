// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text;
using Phantom.Relay.Xbox.Http;

namespace Phantom.Relay.Xbox.Tests.Fakes;

/// <summary>
/// Answers each request with the next reply scripted for its verb and absolute path, and records every request. A
/// request nothing was scripted for fails the test.
/// </summary>
public sealed class ScriptedHttpExchange : IHttpExchange
{
    private readonly Dictionary<string, Queue<HttpReply>> _replies = [];

    public List<HttpRequest> Requests { get; } = [];

    public ScriptedHttpExchange Answer(HttpVerb verb, Uri address, int status, string body)
    {
        string key = Key(verb, address);

        if (!_replies.TryGetValue(key, out var queue))
        {
            queue = new Queue<HttpReply>();
            _replies[key] = queue;
        }

        queue.Enqueue(new HttpReply(status, Encoding.UTF8.GetBytes(body)));

        return this;
    }

    public Task<HttpReply> SendAsync(HttpRequest request, CancellationToken cancellation)
    {
        Requests.Add(request);

        if (!_replies.TryGetValue(Key(request.Verb, request.Address), out var queue) || queue.Count == 0)
        {
            throw new InvalidOperationException($"nothing scripted for {request.Verb} {request.Address}");
        }

        return Task.FromResult(queue.Dequeue());
    }

    public string BodyOf(int index)
    {
        return Encoding.UTF8.GetString(Requests[index].Body.Span);
    }

    private static string Key(HttpVerb verb, Uri address)
    {
        return $"{verb} {address.GetLeftPart(UriPartial.Path)}";
    }
}
