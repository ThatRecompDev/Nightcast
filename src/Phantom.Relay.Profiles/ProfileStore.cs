// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Engine.Content.Storage;

namespace Phantom.Relay.Profiles;

/// <summary>
/// The profiles in the user's store (the engine's <see cref="IPersistentStore"/>), one key per profile:
/// <c>profile.</c> and the name with every character outside the store's key alphabet replaced by '_'.
/// </summary>
public sealed class ProfileStore
{
    public const string KeyPrefix = "profile.";

    private readonly IPersistentStore _store;

    public ProfileStore(IPersistentStore store)
    {
        _store = store;
    }

    public static string KeyOf(string name)
    {
        var key = new System.Text.StringBuilder(KeyPrefix, KeyPrefix.Length + name.Length);

        foreach (char c in name)
        {
            if (char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
            {
                key.Append(c);
            }
            else
            {
                key.Append('_');
            }
        }

        return key.ToString();
    }

    public IReadOnlyList<StreamProfile> LoadAll()
    {
        var profiles = new List<StreamProfile>();

        foreach (string key in _store.Keys())
        {
            if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            byte[]? bytes = _store.Read(key);

            if (bytes is not null)
            {
                profiles.Add(Read(bytes));
            }
        }

        return profiles;
    }

    /// <summary>False when the store refused the write; the old profile then stays.</summary>
    public bool Save(StreamProfile profile)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(profile, ProfilesJsonContext.Default.StreamProfile);

        return _store.Write(KeyOf(profile.Name), bytes);
    }

    public void Delete(string name)
    {
        _store.Delete(KeyOf(name));
    }

    private static StreamProfile Read(byte[] bytes)
    {
        var profile = JsonSerializer.Deserialize(bytes, ProfilesJsonContext.Default.StreamProfile);

        if (profile is null)
        {
            throw new JsonException("a stored profile was empty");
        }

        return profile;
    }
}
