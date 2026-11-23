// Copyright (c) 2026 Phantom.Relay. All rights reserved.

using Microsoft.Extensions.Logging;
using Phantom.Relay.Xbox.Auth.Payloads;
using Phantom.Relay.Xbox.Http;
using Phantom.Relay.Xbox.Json;
using Phantom.Relay.Xbox.Logging;

namespace Phantom.Relay.Xbox.Auth;

/// <summary>
/// The sign-in chain (docs/architecture/xhome.md#sign-in): the Microsoft account code or refresh grant, the Xbox Live
/// user token, XSTS for the streaming service, then the console offering's streaming token. Only the Microsoft account
/// tokens are kept; the rest live in memory and are renewed when they near their expiry.
/// </summary>
public sealed class XboxAuthService : IXboxAuthService, IDisposable
{
    private readonly IHttpExchange _http;
    private readonly CredentialStore _credentials;
    private readonly TimeProvider _clock;
    private readonly ILogger<XboxAuthService> _log;
    private readonly Func<TimeSpan, CancellationToken, Task> _wait;
    /// <summary>RFC 8628: a "slow down" answer adds this to the interval.</summary>
    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private MsaToken? _msa;
    private XblToken? _xsts;
    private StreamingToken? _streaming;

    /// <summary>A sign-in code's checks wait on the clock's timers.</summary>
    public XboxAuthService(IHttpExchange http, CredentialStore credentials, TimeProvider clock, ILogger<XboxAuthService> log)
        : this(http, credentials, clock, log, (delay, cancellation) => Task.Delay(delay, clock, cancellation))
    {
    }

    /// <summary><paramref name="wait"/> waits between a sign-in code's checks (a test's passes at once).</summary>
    internal XboxAuthService(IHttpExchange http, CredentialStore credentials, TimeProvider clock, ILogger<XboxAuthService> log, Func<TimeSpan, CancellationToken, Task> wait)
    {
        _http = http;
        _credentials = credentials;
        _clock = clock;
        _log = log;
        _wait = wait;
    }

    public bool IsSignedIn => _msa is not null || _credentials.Load() is not null;

    public Uri SignInAddress => SignInPage.Address();

    public async Task CompleteSignInAsync(Uri redirected, CancellationToken cancellation)
    {
        string code = SignInPage.ReadCode(redirected);
        var msa = await RequestMsaAsync(Grant("authorization_code", "code", code), cancellation);

        await RememberAsync(msa, cancellation);
    }

    private async Task RememberAsync(MsaToken msa, CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation);

        try
        {
            Remember(msa);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>How much longer to wait before asking again: nothing while the code waits, more after a "slow down"; a refusal ends the sign-in.</summary>
    private static TimeSpan Waited(HttpReply reply)
    {
        var refusal = HttpRequests.Read(reply, RelayJsonContext.Default.MsaErrorReply);
        string? error = refusal?.Error;

        if (error == XboxEndpoints.AuthorizationPending)
        {
            return TimeSpan.Zero;
        }

        if (error == XboxEndpoints.SlowDown)
        {
            return SlowDownStep;
        }

        throw new XboxAuthException($"Sign-in was not completed: {refusal?.Description ?? error ?? $"HTTP {reply.Status}"}");
    }

    private static KeyValuePair<string, string>[] DeviceGrant(string deviceCode)
    {
        return
        [
            new("client_id", XboxEndpoints.MsaClientId),
            new("grant_type", XboxEndpoints.DeviceCodeGrant),
            new("device_code", deviceCode),
        ];
    }

    public async Task<DeviceSignIn> StartDeviceSignInAsync(CancellationToken cancellation)
    {
        KeyValuePair<string, string>[] fields =
        [
            new("client_id", XboxEndpoints.MsaClientId),
            new("scope", XboxEndpoints.MsaScope),
            new("response_type", "device_code"),
        ];
        var reply = await _http.SendAsync(HttpRequests.Form(XboxEndpoints.MsaDeviceCode, fields), cancellation);
        var code = ReadSuccess(reply, RelayJsonContext.Default.DeviceCodeReply, "sign-in code");

        var page = new Uri(Required(code.VerificationUri, "verification_uri"));
        var expiresAt = _clock.GetUtcNow().AddSeconds(code.ExpiresIn);

        return new DeviceSignIn(Required(code.UserCode, "user_code"), page, TimeSpan.FromSeconds(code.Interval), expiresAt)
        {
            DeviceCode = Required(code.DeviceCode, "device_code"),
        };
    }

    /// <summary>
    /// Asks every interval (longer after a "slow down") until the code was entered, then keeps the account like any
    /// sign-in; a refusal or the code's expiry ends it.
    /// </summary>
    public async Task CompleteDeviceSignInAsync(DeviceSignIn signIn, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(signIn);
        var interval = signIn.Interval;

        while (_clock.GetUtcNow() < signIn.ExpiresAt)
        {
            await _wait(interval, cancellation);
            var reply = await _http.SendAsync(HttpRequests.Form(XboxEndpoints.MsaToken, DeviceGrant(signIn.DeviceCode)), cancellation);

            if (reply.IsSuccess)
            {
                await RememberAsync(ToMsaToken(ReadSuccess(reply, RelayJsonContext.Default.MsaTokenReply, "Microsoft account token")), cancellation);

                return;
            }

            interval += Waited(reply);
        }

        throw new XboxAuthException("The sign-in code expired before it was entered. Start again for a new one.");
    }

    public async Task<StreamingToken> GetStreamingTokenAsync(CancellationToken cancellation)
    {
        await _gate.WaitAsync(cancellation);

        try
        {
            if (_streaming is not null && !_streaming.IsExpired(_clock))
            {
                return _streaming;
            }

            var msa = await ValidMsaAsync(cancellation);

            if (_xsts is null || _xsts.IsExpired(_clock))
            {
                var user = await RequestUserTokenAsync(msa, cancellation);
                _xsts = await RequestXstsAsync(user, cancellation);
            }

            _streaming = await RequestStreamingAsync(_xsts, cancellation);

            return _streaming;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    public void SignOut()
    {
        _msa = null;
        _xsts = null;
        _streaming = null;
        _credentials.Clear();
    }

    private static KeyValuePair<string, string>[] Grant(string grantType, string field, string value)
    {
        return
        [
            new("client_id", XboxEndpoints.MsaClientId),
            new("grant_type", grantType),
            new(field, value),
            new("redirect_uri", XboxEndpoints.MsaRedirect.ToString()),
            new("scope", XboxEndpoints.MsaScope),
        ];
    }

    private static HttpHeader[] ContractHeaders()
    {
        return [new HttpHeader(XboxEndpoints.ContractVersionHeader, XboxEndpoints.ContractVersion)];
    }

    private static XblToken ToXblToken(XblTokenReply reply)
    {
        string token = Required(reply.Token, "Token");
        string hash = string.Empty;
        var users = reply.DisplayClaims?.Users;

        if (users is not null && users.Count > 0)
        {
            hash = users[0].UserHash ?? string.Empty;
        }

        return new XblToken(token, hash, reply.NotAfter);
    }

    private static string Required(string? value, string name)
    {
        if (value is null)
        {
            throw new XboxAuthException($"The sign-in service's answer has no {name}.");
        }

        return value;
    }

    /// <summary>A new sign-in: the account may have changed, so everything below it is dropped.</summary>
    private void Remember(MsaToken msa)
    {
        _xsts = null;
        _streaming = null;
        Keep(msa);
    }

    /// <summary>A renewed token of the same account: the XSTS and streaming tokens stay valid until their own expiry.</summary>
    private void Keep(MsaToken msa)
    {
        _msa = msa;

        if (!_credentials.Save(new StoredCredentials(msa)))
        {
            _log.CredentialsNotKept();
        }
    }

    private async Task<MsaToken> ValidMsaAsync(CancellationToken cancellation)
    {
        var msa = _msa ?? _credentials.Load()?.Msa;

        if (msa is null)
        {
            throw new XboxAuthException("Not signed in.");
        }

        if (!msa.IsExpired(_clock))
        {
            _msa = msa;

            return msa;
        }

        _log.RenewingAccountToken();
        var reply = await _http.SendAsync(HttpRequests.Form(XboxEndpoints.MsaToken, Grant("refresh_token", "refresh_token", msa.RefreshToken)), cancellation);
        ForgetWhenRefused(reply);

        var renewed = ToMsaToken(ReadSuccess(reply, RelayJsonContext.Default.MsaTokenReply, "Microsoft account token"));
        Keep(renewed);

        return renewed;
    }

    /// <summary>A kept refresh token the service no longer takes (revoked, a changed password, long unused) is forgotten.</summary>
    private void ForgetWhenRefused(HttpReply reply)
    {
        if (reply.IsSuccess || HttpRequests.Read(reply, RelayJsonContext.Default.MsaErrorReply)?.Error != XboxEndpoints.InvalidGrant)
        {
            return;
        }

        _msa = null;
        _credentials.Clear();

        throw new XboxAuthException("The kept sign-in has expired; sign in again.") { SignInAgain = true };
    }

    private async Task<MsaToken> RequestMsaAsync(KeyValuePair<string, string>[] grant, CancellationToken cancellation)
    {
        var reply = await _http.SendAsync(HttpRequests.Form(XboxEndpoints.MsaToken, grant), cancellation);

        return ToMsaToken(ReadSuccess(reply, RelayJsonContext.Default.MsaTokenReply, "Microsoft account token"));
    }

    private MsaToken ToMsaToken(MsaTokenReply token)
    {
        var expiresAt = _clock.GetUtcNow().AddSeconds(token.ExpiresIn);

        return new MsaToken(Required(token.AccessToken, "access_token"), Required(token.RefreshToken, "refresh_token"), expiresAt);
    }

    private async Task<XblToken> RequestUserTokenAsync(MsaToken msa, CancellationToken cancellation)
    {
        var properties = new UserTokenProperties(
            XboxEndpoints.UserAuthMethod,
            XboxEndpoints.UserSiteName,
            XboxEndpoints.RpsTicketPrefix + msa.AccessToken);
        var body = new UserTokenRequest(XboxEndpoints.UserRelyingParty, XboxEndpoints.JwtTokenType, properties);
        var request = HttpRequests.Json(HttpVerb.Post, XboxEndpoints.UserAuthenticate, ContractHeaders(), body, RelayJsonContext.Default.UserTokenRequest);

        var reply = await _http.SendAsync(request, cancellation);

        return ToXblToken(ReadSuccess(reply, RelayJsonContext.Default.XblTokenReply, "Xbox Live user token"));
    }

    private async Task<XblToken> RequestXstsAsync(XblToken user, CancellationToken cancellation)
    {
        var properties = new XstsProperties(XboxEndpoints.RetailSandbox, [user.Token]);
        var body = new XstsRequest(XboxEndpoints.StreamingRelyingParty, XboxEndpoints.JwtTokenType, properties);
        var request = HttpRequests.Json(HttpVerb.Post, XboxEndpoints.XstsAuthorize, ContractHeaders(), body, RelayJsonContext.Default.XstsRequest);

        var reply = await _http.SendAsync(request, cancellation);

        if (reply.IsUnauthorized)
        {
            var refusal = HttpRequests.Read(reply, RelayJsonContext.Default.XstsErrorReply);

            throw new XboxAuthException(XstsRefusals.Describe(refusal?.Error ?? 0));
        }

        return ToXblToken(ReadSuccess(reply, RelayJsonContext.Default.XblTokenReply, "XSTS token"));
    }

    private async Task<StreamingToken> RequestStreamingAsync(XblToken xsts, CancellationToken cancellation)
    {
        var body = new StreamingLoginRequest(XboxEndpoints.ConsoleOffering, xsts.Token);
        HttpHeader[] headers = [new HttpHeader(XboxEndpoints.StreamingClientHeader, XboxEndpoints.StreamingClient)];
        var request = HttpRequests.Json(HttpVerb.Post, XboxEndpoints.ConsoleLogin, headers, body, RelayJsonContext.Default.StreamingLoginRequest);

        var reply = await _http.SendAsync(request, cancellation);
        var login = ReadSuccess(reply, RelayJsonContext.Default.StreamingLoginReply, "streaming token");
        var regions = StreamingRegions.From(login.OfferingSettings?.Regions);

        if (regions.Count == 0)
        {
            throw new XboxAuthException("The streaming service named no region for remote play.");
        }

        var expiresAt = _clock.GetUtcNow().AddSeconds(login.DurationInSeconds);

        return new StreamingToken(Required(login.StreamingToken, "gsToken"), expiresAt, regions);
    }

    private T ReadSuccess<T>(HttpReply reply, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, string what)
        where T : class
    {
        if (!reply.IsSuccess)
        {
            int status = reply.Status;
            _log.SignInRequestFailed(what, status);

            throw new XboxAuthException($"The {what} request failed (HTTP {reply.Status}).");
        }

        var value = HttpRequests.Read(reply, type);

        if (value is null)
        {
            throw new XboxAuthException($"The {what} answer was empty.");
        }

        return value;
    }
}
