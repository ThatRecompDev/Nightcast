// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>What is kept between runs: the Microsoft account tokens. Everything below them is renewed from them.</summary>
public sealed record StoredCredentials(MsaToken Msa);
