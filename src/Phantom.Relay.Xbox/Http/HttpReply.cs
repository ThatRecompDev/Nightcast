// Copyright (c) 2026 Phantom.Relay. All rights reserved.

namespace Phantom.Relay.Xbox.Http;

/// <summary>An answer: its status code and body.</summary>
public sealed record HttpReply(int Status, ReadOnlyMemory<byte> Body)
{
    private const int FirstSuccess = 200;
    private const int FirstRedirect = 300;
    private const int NoContentStatus = 204;
    private const int UnauthorizedStatus = 401;

    public bool IsSuccess => Status >= FirstSuccess && Status < FirstRedirect;

    /// <summary>A success without a body: what the service answers while a result is not ready yet.</summary>
    public bool IsEmpty => Status == NoContentStatus || Body.IsEmpty;

    public bool IsUnauthorized => Status == UnauthorizedStatus;
}
