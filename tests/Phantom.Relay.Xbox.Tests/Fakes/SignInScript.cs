// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Http;

namespace Phantom.Relay.Xbox.Tests.Fakes;

/// <summary>Scripts a whole sign-in chain with made-up tokens and one region.</summary>
public static class SignInScript
{
    public static readonly Uri RegionBase = new("https://region.example.test/");

    public static ScriptedHttpExchange MsaGrant(this ScriptedHttpExchange http, string access, int expiresIn)
    {
        return http.Answer(
            HttpVerb.Post,
            XboxEndpoints.MsaToken,
            200,
            $$"""{"access_token":"{{access}}","refresh_token":"refresh-1","expires_in":{{expiresIn}}}""");
    }

    /// <summary>A sign-in code, as the live service answers it (made-up codes).</summary>
    public static ScriptedHttpExchange DeviceCode(this ScriptedHttpExchange http, int expiresIn = 900)
    {
        return http.Answer(
            HttpVerb.Post,
            XboxEndpoints.MsaDeviceCode,
            200,
            $$"""{"user_code":"ABCD1234","device_code":"device-secret","verification_uri":"https://www.microsoft.com/link","interval":5,"expires_in":{{expiresIn}}}""");
    }

    /// <summary>The token endpoint's answer while the code was not entered yet.</summary>
    public static ScriptedHttpExchange Pending(this ScriptedHttpExchange http)
    {
        return http.Answer(HttpVerb.Post, XboxEndpoints.MsaToken, 400, """{"error":"authorization_pending"}""");
    }

    public static ScriptedHttpExchange TokenChain(this ScriptedHttpExchange http)
    {
        return http
            .Answer(HttpVerb.Post, XboxEndpoints.UserAuthenticate, 200, """{"Token":"user-token","NotAfter":"2026-12-01T00:00:00.0000000Z","DisplayClaims":{"xui":[{"uhs":"hash"}]}}""")
            .Answer(HttpVerb.Post, XboxEndpoints.XstsAuthorize, 200, """{"Token":"xsts-token","NotAfter":"2026-12-01T00:00:00Z","DisplayClaims":{"xui":[{"uhs":"hash"}]}}""")
            .StreamingLogin();
    }

    public static ScriptedHttpExchange StreamingLogin(this ScriptedHttpExchange http)
    {
        return http.Answer(
            HttpVerb.Post,
            XboxEndpoints.ConsoleLogin,
            200,
            $$$"""{"gsToken":"gs-token","durationInSeconds":3600,"offeringSettings":{"regions":[{"name":"Other","baseUri":"https://other.example.test","isDefault":false},{"name":"Home","baseUri":"{{{RegionBase}}}","isDefault":true}]}}""");
    }
}
