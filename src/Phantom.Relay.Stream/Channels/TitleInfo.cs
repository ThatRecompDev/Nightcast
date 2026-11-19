// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Relay.Xbox.Channels;

namespace Phantom.Relay.Stream.Channels;

/// <summary>
/// The running game, read from the console's <see cref="XHomeChannels.TitleInfoTarget"/> message on the message
/// channel. The message was seen live; its content's shape was not (the probe keeps only message kinds), so the title id
/// is taken from a <c>titleId</c> field of the content in any letter case, a string or a number, and the content may be
/// JSON in a string (as the other messages carry it) or an object. Anything else reads as no title.
/// </summary>
public static class TitleInfo
{
    private const string TargetField = "target";
    private const string ContentField = "content";
    private const string TitleIdField = "titleId";

    /// <summary>The title id the message names, or null when it is not the title message or names none.</summary>
    public static string? Read(string message)
    {
        try
        {
            using var envelope = JsonDocument.Parse(message);
            var root = envelope.RootElement;

            if (root.ValueKind != JsonValueKind.Object || !IsTitleInfo(root) || !root.TryGetProperty(ContentField, out var content))
            {
                return null;
            }

            return FromContent(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsTitleInfo(JsonElement root)
    {
        return root.TryGetProperty(TargetField, out var target)
            && target.ValueKind == JsonValueKind.String
            && string.Equals(target.GetString(), XHomeChannels.TitleInfoTarget, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FromContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.Object)
        {
            return TitleIdOf(content);
        }

        if (content.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        using var inner = JsonDocument.Parse(content.GetString() ?? string.Empty);

        return TitleIdOf(inner.RootElement);
    }

    private static string? TitleIdOf(JsonElement content)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var field in content.EnumerateObject())
        {
            if (!string.Equals(field.Name, TitleIdField, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return Text(field.Value);
        }

        return null;
    }

    private static string? Text(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number)
        {
            return value.GetRawText();
        }

        if (value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        string? text = value.GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return text;
    }
}
