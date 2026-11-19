// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using System.Text.Json;
using Phantom.Engine.Content.Storage;
using Phantom.Relay.Xbox.Json;

namespace Phantom.Relay.Xbox.Auth;

/// <summary>
/// Keeps the account's tokens in the user's store (the engine's <see cref="IPersistentStore"/>: a folder of the app's
/// own on the desktop, the page's storage in the browser).
/// </summary>
public sealed class CredentialStore
{
    public const string Key = "account.credentials";

    private readonly IPersistentStore _store;

    public CredentialStore(IPersistentStore store)
    {
        _store = store;
    }

    public StoredCredentials? Load()
    {
        byte[]? bytes = _store.Read(Key);

        if (bytes is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(bytes, RelayJsonContext.Default.StoredCredentials);
    }

    /// <summary>False when the store refused the write (full or unavailable); the old value then stays.</summary>
    public bool Save(StoredCredentials credentials)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(credentials, RelayJsonContext.Default.StoredCredentials);

        return _store.Write(Key, bytes);
    }

    public void Clear()
    {
        _store.Delete(Key);
    }
}
