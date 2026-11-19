// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Phantom.Relay.Xbox.Http;

/// <summary>Builds requests with JSON or form bodies, serialized through source-generated metadata only.</summary>
public static class HttpRequests
{
    public const string JsonType = "application/json";
    public const string FormType = "application/x-www-form-urlencoded";

    public static HttpRequest Json<T>(HttpVerb verb, Uri address, IReadOnlyList<HttpHeader> headers, T body, JsonTypeInfo<T> type)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(body, type);

        return new HttpRequest(verb, address, headers, bytes, JsonType);
    }

    public static HttpRequest Empty(HttpVerb verb, Uri address, IReadOnlyList<HttpHeader> headers)
    {
        return new HttpRequest(verb, address, headers, ReadOnlyMemory<byte>.Empty, null);
    }

    public static HttpRequest Form(Uri address, IReadOnlyList<KeyValuePair<string, string>> fields)
    {
        var text = new System.Text.StringBuilder();

        foreach (var field in fields)
        {
            if (text.Length > 0)
            {
                text.Append('&');
            }

            text.Append(Uri.EscapeDataString(field.Key)).Append('=').Append(Uri.EscapeDataString(field.Value));
        }

        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(text.ToString());

        return new HttpRequest(HttpVerb.Post, address, [], bytes, FormType);
    }

    /// <summary>Reads a JSON body; null when the body is empty.</summary>
    public static T? Read<T>(HttpReply reply, JsonTypeInfo<T> type)
        where T : class
    {
        if (reply.IsEmpty)
        {
            return null;
        }

        return JsonSerializer.Deserialize(reply.Body.Span, type);
    }
}
