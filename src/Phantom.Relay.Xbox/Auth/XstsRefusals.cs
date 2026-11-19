// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>Puts an XSTS refusal (XErr) in the player's words.</summary>
public static class XstsRefusals
{
    public static string Describe(long error)
    {
        switch (error)
        {
            case XboxEndpoints.NoXboxProfile:
                return "This Microsoft account has no Xbox profile yet. Sign in on xbox.com once, then try again.";
            case XboxEndpoints.CountryNotServed:
                return "Xbox Live is not offered in this account's country.";
            case XboxEndpoints.ChildAccount:
                return "This is a child account: an adult must add it to a Microsoft family first.";
            default:
                return $"Xbox Live refused this account for streaming (XErr {error}).";
        }
    }
}
