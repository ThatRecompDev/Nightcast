// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox;

/// <summary>
/// Every endpoint and constant of the sign-in chain and the streaming service, in one place
/// (docs/architecture/xhome.md). None of them is documented by Microsoft; they are what the public web player and the
/// Xbox app send.
/// </summary>
public static class XboxEndpoints
{
    /// <summary>The Xbox app's public client id, which the Microsoft account sign-in accepts for Xbox Live scopes.</summary>
    public const string MsaClientId = "000000004C12AE6F";

    public const string MsaScope = "service::user.auth.xboxlive.com::MBI_SSL";

    public const string UserRelyingParty = "http://auth.xboxlive.com";

    public const string UserSiteName = "user.auth.xboxlive.com";

    public const string UserAuthMethod = "RPS";

    /// <summary>The prefix of a Microsoft account ticket issued to a public client (a confidential app's is "d=").</summary>
    public const string RpsTicketPrefix = "t=";

    public const string StreamingRelyingParty = "http://gssv.xboxlive.com/";

    public const string RetailSandbox = "RETAIL";

    public const string JwtTokenType = "JWT";

    public const string ConsoleOffering = "xhome";

    public const string ContractVersionHeader = "x-xbl-contract-version";

    public const string ContractVersion = "1";

    /// <summary>The client name the web player reports to the streaming service.</summary>
    public const string StreamingClientHeader = "x-gssv-client";

    public const string StreamingClient = "XboxComBrowser";

    public const string DeviceInfoHeader = "x-ms-device-info";

    public const string AuthorizationHeader = "Authorization";

    public const string BearerPrefix = "Bearer ";

    /// <summary>XSTS refusals the player can act on (the XErr field of a 401).</summary>
    public const long NoXboxProfile = 2148916233;

    public const long CountryNotServed = 2148916235;

    public const long ChildAccount = 2148916238;

    public static readonly Uri MsaRedirect = new("https://login.live.com/oauth20_desktop.srf");

    public static readonly Uri MsaAuthorize = new("https://login.live.com/oauth20_authorize.srf");

    public static readonly Uri MsaToken = new("https://login.live.com/oauth20_token.srf");

    public static readonly Uri UserAuthenticate = new("https://user.auth.xboxlive.com/user/authenticate");

    public static readonly Uri XstsAuthorize = new("https://xsts.auth.xboxlive.com/xsts/authorize");

    public static readonly Uri ConsoleLogin = new("https://xhome.gssv-play-prod.xboxlive.com/v2/login/user");

    /// <summary>A token is renewed this long before it expires, so no request goes out with one about to lapse.</summary>
    public static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(2);
}
