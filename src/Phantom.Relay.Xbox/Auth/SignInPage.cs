// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Auth;

/// <summary>
/// The Microsoft account sign-in page and the code it hands back. The page ends on <see cref="XboxEndpoints.MsaRedirect"/>
/// with <c>?code=...</c> (or <c>?error=...</c>); a head shows the page and passes that final address back.
/// </summary>
public static class SignInPage
{
    private const string CodeField = "code";
    private const string ErrorField = "error";
    private const string ErrorDescriptionField = "error_description";

    public static Uri Address()
    {
        var query = string.Join(
            '&',
            Field("client_id", XboxEndpoints.MsaClientId),
            Field("redirect_uri", XboxEndpoints.MsaRedirect.ToString()),
            Field("response_type", CodeField),
            Field("scope", XboxEndpoints.MsaScope),
            Field("display", "touch"));

        return new UriBuilder(XboxEndpoints.MsaAuthorize) { Query = query }.Uri;
    }

    /// <summary>The authorization code from the final address; throws with the page's reason when it has none.</summary>
    public static string ReadCode(Uri redirected)
    {
        string? error = QueryValue(redirected, ErrorField);

        if (error is not null)
        {
            string detail = QueryValue(redirected, ErrorDescriptionField) ?? error;

            throw new XboxAuthException($"Sign-in was not completed: {detail}");
        }

        string? code = QueryValue(redirected, CodeField);

        if (code is null)
        {
            throw new XboxAuthException("The sign-in page returned no code. Paste the whole address it ended on.");
        }

        return code;
    }

    private static string Field(string name, string value)
    {
        return $"{name}={Uri.EscapeDataString(value)}";
    }

    private static string? QueryValue(Uri address, string name)
    {
        var query = address.Query.AsSpan().TrimStart('?');

        foreach (var range in query.Split('&'))
        {
            var pair = query[range];
            int equals = pair.IndexOf('=');

            if (equals < 0 || !pair[..equals].SequenceEqual(name))
            {
                continue;
            }

            return Uri.UnescapeDataString(pair[(equals + 1)..].ToString().Replace('+', ' '));
        }

        return null;
    }
}
