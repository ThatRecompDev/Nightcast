// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Phantom.Relay.Xbox.Auth;

namespace Phantom.Relay.Xbox.Tests.Auth;

public sealed class SignInPageTests
{
    [Fact]
    public void The_address_asks_for_a_code_for_the_xbox_live_scope()
    {
        var address = SignInPage.Address();

        Assert.Equal("login.live.com", address.Host);
        Assert.Contains("client_id=000000004C12AE6F", address.Query, StringComparison.Ordinal);
        Assert.Contains("response_type=code", address.Query, StringComparison.Ordinal);
        Assert.Contains("scope=service%3A%3Auser.auth.xboxlive.com%3A%3AMBI_SSL", address.Query, StringComparison.Ordinal);
    }

    [Fact]
    public void The_code_is_read_from_the_final_address()
    {
        var redirected = new Uri("https://login.live.com/oauth20_desktop.srf?lc=1033&code=M.C5_BAY.2.U.abc%2Bdef");

        string code = SignInPage.ReadCode(redirected);

        Assert.Equal("M.C5_BAY.2.U.abc+def", code);
    }

    [Fact]
    public void A_refused_sign_in_throws_with_the_pages_reason()
    {
        var redirected = new Uri("https://login.live.com/oauth20_desktop.srf?error=access_denied&error_description=The+user+declined");

        var error = Assert.Throws<XboxAuthException>(() => SignInPage.ReadCode(redirected));

        Assert.Contains("The user declined", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_address_without_a_code_throws()
    {
        var redirected = new Uri("https://login.live.com/oauth20_desktop.srf?lc=1033");

        Assert.Throws<XboxAuthException>(() => SignInPage.ReadCode(redirected));
    }
}
